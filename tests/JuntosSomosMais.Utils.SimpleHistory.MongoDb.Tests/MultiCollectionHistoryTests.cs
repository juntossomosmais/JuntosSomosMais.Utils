using JuntosSomosMais.Utils.SimpleHistory.MongoDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class MultiCollectionHistoryTests : MongoTestBase
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(20);

    private static async Task<T> WaitForAsync<T>(Func<Task<T>> probe, Func<T, bool> done)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        var value = await probe();
        while (!done(value))
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the expected state.");
            await Task.Delay(100);
            value = await probe();
        }

        return value;
    }

    [Fact]
    public async Task TrackingTwoCollections_ShouldKeepEachHistorySeparateAndReplayable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddMongoHistory(_ => Database, history => history
            .Track("orders", o =>
            {
                o.IgnoreFields("CreationDate");
                o.MaxAwaitTime = TimeSpan.FromMilliseconds(200);
            })
            .Track("carts", o => o.MaxAwaitTime = TimeSpan.FromMilliseconds(200)));
        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().ToList();
        Assert.Equal(2, hosted.Count);

        var checkpoints = Database.GetCollection<BsonDocument>("simpleHistoryCheckpoint");
        var orders = Database.GetCollection<BsonDocument>("orders");
        var carts = Database.GetCollection<BsonDocument>("carts");
        var ordersHistory = Database.GetCollection<HistoryEntry>("ordersHistory");
        var cartsHistory = Database.GetCollection<HistoryEntry>("cartsHistory");

        try
        {
            foreach (var service in hosted)
                await service.StartAsync(CancellationToken.None);

            // Both streams are open once each source saved a position (empty batches are checkpointed too).
            await WaitForAsync(
                () => checkpoints.CountDocumentsAsync(Builders<BsonDocument>.Filter.Exists("ResumeToken")),
                count => count == 2);

            var orderId = ObjectId.GenerateNewId();
            await orders.InsertOneAsync(new BsonDocument { { "_id", orderId }, { "StatusOrder", "NEW" }, { "CreationDate", 1 } });
            await orders.ReplaceOneAsync(new BsonDocument("_id", orderId), new BsonDocument { { "StatusOrder", "PAID" }, { "CreationDate", 2 } });
            await orders.UpdateOneAsync(new BsonDocument("_id", orderId), new BsonDocument("$set", new BsonDocument("Total", 99)));
            await carts.InsertOneAsync(new BsonDocument { { "_id", "cart-1" }, { "Items", 1 }, { "CreationDate", 1 } });
            await carts.UpdateOneAsync(new BsonDocument("_id", "cart-1"), new BsonDocument("$set", new BsonDocument { { "CreationDate", 2 }, { "Items", 2 } }));

            var orderRows = await WaitForAsync(
                () => ordersHistory.Find(FilterDefinition<HistoryEntry>.Empty).SortBy(h => h.HistoryDate).ThenBy(h => h.ChangeToken).ToListAsync(),
                rows => rows.Count >= 3);
            var cartRows = await WaitForAsync(
                () => cartsHistory.Find(FilterDefinition<HistoryEntry>.Empty).SortBy(h => h.HistoryDate).ThenBy(h => h.ChangeToken).ToListAsync(),
                rows => rows.Count >= 2);
            await Task.Delay(500);

            // Each history collection holds only its own rows.
            Assert.Equal(3, await ordersHistory.CountDocumentsAsync(FilterDefinition<HistoryEntry>.Empty));
            Assert.Equal(2, await cartsHistory.CountDocumentsAsync(FilterDefinition<HistoryEntry>.Empty));
            Assert.All(orderRows, r => Assert.Equal(new BsonObjectId(orderId), r.EntityId));
            Assert.All(cartRows, r => Assert.Equal(new BsonString("cart-1"), r.EntityId));

            // orders ignores CreationDate in deltas, carts keeps it.
            Assert.Equal<string>(["+", "~", "~"], orderRows.Select(r => r.HistoryType));
            Assert.DoesNotContain(orderRows.Skip(1), r => r.Data.Contains("CreationDate"));
            Assert.Equal(2, cartRows[1].Data.ElementCount);
            Assert.Equal(2, cartRows[1].Data["CreationDate"].AsInt32);
            Assert.Equal(2, cartRows[1].Data["Items"].AsInt32);

            // Replay reproduces each current document (orders: modulo the ignored field).
            var currentOrder = await orders.Find(new BsonDocument("_id", orderId)).FirstAsync();
            currentOrder.Remove("_id");
            currentOrder.Remove("CreationDate");
            var rebuiltOrder = HistoryReplay.Rebuild(orderRows);
            rebuiltOrder.Remove("CreationDate");
            Assert.Equal(currentOrder, rebuiltOrder);
            var currentCart = await carts.Find(new BsonDocument("_id", "cart-1")).FirstAsync();
            currentCart.Remove("_id");
            Assert.Equal(currentCart, HistoryReplay.Rebuild(cartRows));

            // One shared checkpoint collection, one document per source.
            using var names = await Database.ListCollectionNamesAsync(new ListCollectionNamesOptions { Filter = new BsonDocument("name", new BsonRegularExpression("heckpoint")) });
            Assert.Equal<string>(["simpleHistoryCheckpoint"], await names.ToListAsync());
            var ids = (await checkpoints.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync()).Select(d => d["_id"].AsString).OrderBy(i => i);
            Assert.Equal<string>(["carts", "orders"], ids);
        }
        finally
        {
            foreach (var service in hosted)
                await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15));
        }
    }
}
