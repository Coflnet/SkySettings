using System.Threading.Tasks;
using System;
using System.Linq;
using StackExchange.Redis;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Cassandra;
using Cassandra.Data.Linq;
using ISession = Cassandra.ISession;
using Prometheus;
using Microsoft.Extensions.Logging;
using System.Threading;
using Cassandra.Mapping;

namespace Coflnet.Sky.Settings.Services;
public class MigrationHandler<T, ToT>
{
    Func<Table<T>> oldTableFactory;
    Func<Table<ToT>> newTableFactory;
    ISession session;
    ILogger<MigrationHandler<T, ToT>> logger;
    private readonly ConnectionMultiplexer redis;
    Counter migrated;
    private int pageSize = 1000;
    Func<T, ToT> map;

    public MigrationHandler(Func<Table<T>> oldTableFactory, ISession session, ILogger<MigrationHandler<T, ToT>> logger, ConnectionMultiplexer redis, Func<Table<ToT>> newTableFactory, Func<T, ToT> map)
    {
        this.oldTableFactory = oldTableFactory;
        this.session = session;
        this.logger = logger;
        this.redis = redis;
        this.newTableFactory = newTableFactory;
        this.map = map;
    }

    SemaphoreSlim queryThrottle = new SemaphoreSlim(7);
    public async Task Migrate(CancellationToken stoppingToken = default)
    {
        newTableFactory().CreateIfNotExists();
        var tableName = newTableFactory().Name;
        var prefix = $"c_migration_{tableName}_";
        migrated = Metrics.CreateCounter($"{prefix}migrated", "The number of items migrated");
        var db = redis.GetDatabase();
        var pagingSateRedis = db.StringGet($"{prefix}paging_state");
        byte[]? pagingState;
        var offset = 0;
        IPage<T> page;
        if (!pagingSateRedis.IsNullOrEmpty)
        {
            pagingState = Convert.FromBase64String(pagingSateRedis!);
            page = await GetOldTable(pagingState);
        }
        else
        {
            page = await GetOldTable([]);
        }
        var fromRedis = db.StringGet($"{prefix}offset");
        if (!fromRedis.IsNullOrEmpty)
        {
            offset = int.Parse(fromRedis);
            logger.LogInformation("Resuming migration of {table} from {0}", tableName, offset);
        }
        do
        {
            _ = Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        await queryThrottle.WaitAsync();
                        var insertCount = await InsertBatch(prefix, db, offset, page, i);
                        Interlocked.Add(ref offset, insertCount);
                        return;
                    }
                    catch (System.Exception e)
                    {
                        logger.LogError(e, "Batch insert failed, {attempt}", i);
                        await Task.Delay(2000 * i, stoppingToken);
                    }
                    finally
                    {
                        queryThrottle.Release();
                    }
                }
            });
            pagingState = page.PagingState;
            logger.LogInformation("Migrated batch {0} of {table}", offset, tableName);
            await queryThrottle.WaitAsync(stoppingToken);
            page = await GetOldTable(pagingState);
            queryThrottle.Release();
        } while (page != null && !stoppingToken.IsCancellationRequested);

        logger.LogInformation("Migration for {tableName} done", tableName);
    }

    private async Task<int> InsertBatch(string prefix, IDatabase db, int offset, IPage<T> page, int attempt = 0)
    {
        var batchToInsert = page;
        var batches = Batch(batchToInsert, (int)Math.Max(1 / Math.Pow(2, attempt), 1));
        await Parallel.ForEachAsync(batches, new ParallelOptions() { MaxDegreeOfParallelism = 5 }, async (batch, c) =>
        {
            try
            {
                await InsertChunk(batch);
            }
            catch (System.Exception)
            {
                if (attempt >= 5)
                    logger.LogError("Insert failed, {Json}", Newtonsoft.Json.JsonConvert.SerializeObject(batch));
                throw;
            }
        });
        offset = UpdateMigrateState(prefix, db, offset, batchToInsert);

        return batchToInsert.Count;
    }

    private int UpdateMigrateState(string prefix, IDatabase db, int offset, IPage<T> batchToInsert)
    {
        migrated.Inc(batchToInsert.Count);
        offset += batchToInsert.Count;
        db.StringSet($"{prefix}offset", offset);
        var queryState = batchToInsert.PagingState;
        if (queryState != null)
        {
            db.StringSet($"{prefix}paging_state", Convert.ToBase64String(queryState));
        }

        return offset;
    }

    private IEnumerable<IEnumerable<T>> Batch(IEnumerable<T> values, int batchSize)
    {
        var list = new List<T>(batchSize);
        foreach (var value in values)
        {
            if (value == null)
                continue;
            list.Add(value);
            if (list.Count == batchSize)
            {
                yield return list;
                list = new List<T>(batchSize);
            }
        }

        if (list.Count > 0)
        {
            yield return list;
        }
    }

    private async Task InsertChunk(IEnumerable<T> batchToInsert)
    {
        var newTable = newTableFactory();
        var batchStatement = new BatchStatement();
        foreach (var score in batchToInsert)
        {
            var mapped = map(score);
            if (mapped == null)
                continue;
            var insert = newTable.Insert(mapped);
            insert.SetTimestamp(new DateTimeOffset(2024, 8, 31, 1, 1, 1, TimeSpan.Zero));
            batchStatement.Add(insert);
        }
        batchStatement.SetConsistencyLevel(ConsistencyLevel.Quorum);
        await session.ExecuteAsync(batchStatement);
    }

    private async Task<IPage<T>> GetOldTable(byte[]? pagingState = null)
    {
        var query = oldTableFactory();
        query.SetPageSize(pageSize);
        query.SetAutoPage(false);
        if (pagingState == null)
            return null;
        if (pagingState.Length != 0)
            query.SetPagingState(pagingState);
        return await query.ExecutePagedAsync();
    }
}

