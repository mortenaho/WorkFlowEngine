using System.Text.Json;
using Npgsql;

namespace WorkflowEngine;

public sealed class PostgresStore : IStore, IAsyncDisposable, IDisposable
{
    private readonly NpgsqlDataSource _ds;

    private PostgresStore(NpgsqlDataSource ds) => _ds = ds;

    public static async Task<PostgresStore> Open(string dsn, CancellationToken cancellationToken = default)
    {
        var ds = NpgsqlDataSource.Create(dsn);
        var store = new PostgresStore(ds);
        try
        {
            await store.Migrate(cancellationToken);
        }
        catch
        {
            await ds.DisposeAsync();
            throw;
        }
        return store;
    }

    public void Dispose() => _ds.Dispose();
    public ValueTask DisposeAsync() => _ds.DisposeAsync();

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS definitions (
          id TEXT PRIMARY KEY,
          tenant_id TEXT NOT NULL DEFAULT 'default',
          key TEXT NOT NULL,
          name TEXT NOT NULL DEFAULT '',
          version INT NOT NULL DEFAULT 1,
          graph JSONB NOT NULL DEFAULT '{}'::jsonb,
          published BOOLEAN NOT NULL DEFAULT TRUE,
          created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        CREATE TABLE IF NOT EXISTS instances (
          id TEXT PRIMARY KEY,
          tenant_id TEXT NOT NULL DEFAULT 'default',
          definition_id TEXT NOT NULL,
          definition_key TEXT NOT NULL,
          parent_instance_id TEXT NOT NULL DEFAULT '',
          status TEXT NOT NULL,
          vars JSONB NOT NULL DEFAULT '{}'::jsonb,
          started_by TEXT NOT NULL,
          version INT NOT NULL DEFAULT 1,
          created_at TIMESTAMPTZ NOT NULL,
          updated_at TIMESTAMPTZ NOT NULL
        );
        CREATE TABLE IF NOT EXISTS tasks (
          id TEXT PRIMARY KEY,
          tenant_id TEXT NOT NULL DEFAULT 'default',
          instance_id TEXT NOT NULL,
          parent_instance_id TEXT NOT NULL DEFAULT '',
          definition_key TEXT NOT NULL DEFAULT '',
          node_id TEXT NOT NULL DEFAULT '',
          token_id TEXT NOT NULL DEFAULT '',
          title TEXT NOT NULL DEFAULT '',
          note TEXT NOT NULL DEFAULT '',
          assignee_kind TEXT NOT NULL,
          assignee_id TEXT NOT NULL,
          claimed_by TEXT NOT NULL DEFAULT '',
          assigned_by TEXT NOT NULL DEFAULT '',
          status TEXT NOT NULL,
          group_mode TEXT NOT NULL DEFAULT '',
          return_reason TEXT NOT NULL DEFAULT '',
          created_at TIMESTAMPTZ NOT NULL,
          updated_at TIMESTAMPTZ NOT NULL,
          completed_at TIMESTAMPTZ
        );
        CREATE INDEX IF NOT EXISTS tasks_instance_idx ON tasks(instance_id);
        CREATE INDEX IF NOT EXISTS tasks_assignee_idx ON tasks(assignee_kind, assignee_id, status);
        CREATE INDEX IF NOT EXISTS tasks_parent_idx ON tasks(parent_instance_id);
        CREATE INDEX IF NOT EXISTS instances_process_idx ON instances(tenant_id, definition_key, parent_instance_id);
        """;

    private const string Alters = """
        ALTER TABLE definitions ADD COLUMN IF NOT EXISTS tenant_id TEXT NOT NULL DEFAULT 'default';
        ALTER TABLE definitions ADD COLUMN IF NOT EXISTS name TEXT NOT NULL DEFAULT '';
        ALTER TABLE definitions ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT NOW();
        ALTER TABLE instances ADD COLUMN IF NOT EXISTS tenant_id TEXT NOT NULL DEFAULT 'default';
        ALTER TABLE instances ADD COLUMN IF NOT EXISTS parent_instance_id TEXT NOT NULL DEFAULT '';
        ALTER TABLE tasks ADD COLUMN IF NOT EXISTS tenant_id TEXT NOT NULL DEFAULT 'default';
        ALTER TABLE tasks ADD COLUMN IF NOT EXISTS parent_instance_id TEXT NOT NULL DEFAULT '';
        ALTER TABLE tasks ADD COLUMN IF NOT EXISTS definition_key TEXT NOT NULL DEFAULT '';
        ALTER TABLE tasks ADD COLUMN IF NOT EXISTS title TEXT NOT NULL DEFAULT '';
        ALTER TABLE tasks ADD COLUMN IF NOT EXISTS note TEXT NOT NULL DEFAULT '';
        ALTER TABLE tasks ADD COLUMN IF NOT EXISTS completed_at TIMESTAMPTZ;
        """;

    private async Task Migrate(CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using (var cmd = new NpgsqlCommand(Schema, conn))
            await cmd.ExecuteNonQueryAsync(ct);
        await using (var cmd = new NpgsqlCommand(Alters, conn))
            await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string EncodeVars(Dictionary<string, object?>? v)
        => JsonSerializer.Serialize(v ?? []);

    private static Dictionary<string, object?> DecodeVars(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return [];
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(raw) ?? [];
    }

    private static Dictionary<string, object?> ReadVars(NpgsqlDataReader row, int ordinal)
    {
        if (row.IsDBNull(ordinal))
            return [];
        var val = row.GetValue(ordinal);
        return val switch
        {
            string s => DecodeVars(s),
            JsonDocument doc => DecodeVars(doc.RootElement.GetRawText()),
            JsonElement el => DecodeVars(el.GetRawText()),
            _ => DecodeVars(val.ToString()),
        };
    }

    public async Task SaveDefinition(Definition def, CancellationToken cancellationToken = default)
    {
        await using var cmd = _ds.CreateCommand("""
            INSERT INTO definitions (id, tenant_id, key, name, version, graph, published, created_at)
            VALUES ($1,$2,$3,$4,1,'{}'::jsonb,TRUE,$5)
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name
            """);
        cmd.Parameters.AddWithValue(def.Id);
        cmd.Parameters.AddWithValue(TenantContext.Normalize(def.TenantId));
        cmd.Parameters.AddWithValue(def.Key);
        cmd.Parameters.AddWithValue(def.Name);
        cmd.Parameters.AddWithValue(def.CreatedAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Definition> GetDefinition(string id, CancellationToken cancellationToken = default)
    {
        await using var cmd = _ds.CreateCommand("""
            SELECT id, tenant_id, key, name, created_at FROM definitions WHERE id=$1
            """);
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw EngineException.NotFound();
        return ScanDef(reader);
    }

    public async Task<Definition?> GetDefinitionByKey(string tenantId, string key, CancellationToken cancellationToken = default)
    {
        await using var cmd = _ds.CreateCommand("""
            SELECT id, tenant_id, key, name, created_at
            FROM definitions
            WHERE tenant_id=$1 AND key=$2
            ORDER BY created_at DESC
            LIMIT 1
            """);
        cmd.Parameters.AddWithValue(TenantContext.Normalize(tenantId));
        cmd.Parameters.AddWithValue(key);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return ScanDef(reader);
    }

    private static Definition ScanDef(NpgsqlDataReader row) => new()
    {
        Id = row.GetString(0),
        TenantId = row.GetString(1),
        Key = row.GetString(2),
        Name = row.GetString(3),
        CreatedAt = Utc(row.GetDateTime(4)),
    };

    public async Task CreateInstance(ProcessInstance inst, CancellationToken cancellationToken = default)
    {
        await using var cmd = _ds.CreateCommand("""
            INSERT INTO instances (id, tenant_id, definition_id, definition_key, parent_instance_id, status, vars, started_by, version, created_at, updated_at)
            VALUES ($1,$2,$3,$4,$5,$6,$7::jsonb,$8,1,$9,$10)
            """);
        cmd.Parameters.AddWithValue(inst.Id);
        cmd.Parameters.AddWithValue(TenantContext.Normalize(inst.TenantId));
        cmd.Parameters.AddWithValue(inst.DefinitionId);
        cmd.Parameters.AddWithValue(inst.DefinitionKey);
        cmd.Parameters.AddWithValue(inst.ParentInstanceId);
        cmd.Parameters.AddWithValue(inst.Status);
        cmd.Parameters.AddWithValue(EncodeVars(inst.Parameters));
        cmd.Parameters.AddWithValue(inst.StartedBy);
        cmd.Parameters.AddWithValue(inst.CreatedAt);
        cmd.Parameters.AddWithValue(inst.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ProcessInstance> GetInstance(string id, CancellationToken cancellationToken = default)
    {
        await using var cmd = _ds.CreateCommand("""
            SELECT id, tenant_id, definition_id, definition_key, parent_instance_id, status, vars, started_by, created_at, updated_at
            FROM instances WHERE id=$1
            """);
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw EngineException.NotFound();
        return ScanInst(reader);
    }

    public async Task UpdateInstance(ProcessInstance inst, CancellationToken cancellationToken = default)
    {
        await using var cmd = _ds.CreateCommand("""
            UPDATE instances SET status=$2, vars=$3::jsonb, updated_at=$4 WHERE id=$1
            """);
        cmd.Parameters.AddWithValue(inst.Id);
        cmd.Parameters.AddWithValue(inst.Status);
        cmd.Parameters.AddWithValue(EncodeVars(inst.Parameters));
        cmd.Parameters.AddWithValue(inst.UpdatedAt);
        var n = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (n == 0)
            throw EngineException.NotFound();
    }

    private static ProcessInstance ScanInst(NpgsqlDataReader row) => new()
    {
        Id = row.GetString(0),
        TenantId = row.GetString(1),
        DefinitionId = row.GetString(2),
        DefinitionKey = row.GetString(3),
        ParentInstanceId = row.GetString(4),
        Status = row.GetString(5),
        Parameters = ReadVars(row, 6),
        StartedBy = row.GetString(7),
        CreatedAt = Utc(row.GetDateTime(8)),
        UpdatedAt = Utc(row.GetDateTime(9)),
    };

    public async Task<IReadOnlyList<ProcessInstance>> ListRootInstances(string tenantId, string processKey, CancellationToken cancellationToken = default)
    {
        await using var cmd = _ds.CreateCommand("""
            SELECT id, tenant_id, definition_id, definition_key, parent_instance_id, status, vars, started_by, created_at, updated_at
            FROM instances
            WHERE tenant_id=$1 AND definition_key=$2 AND parent_instance_id=''
            ORDER BY created_at DESC
            """);
        cmd.Parameters.AddWithValue(TenantContext.Normalize(tenantId));
        cmd.Parameters.AddWithValue(processKey);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var outList = new List<ProcessInstance>();
        while (await reader.ReadAsync(cancellationToken))
            outList.Add(ScanInst(reader));
        return outList;
    }

    public async Task SaveTask(WorkflowTask task, CancellationToken cancellationToken = default)
    {
        await using var cmd = _ds.CreateCommand("""
            INSERT INTO tasks (id, tenant_id, instance_id, parent_instance_id, definition_key, title, note,
              assignee_kind, assignee_id, assigned_by, claimed_by, status, created_at, updated_at, completed_at)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)
            """);
        cmd.Parameters.AddWithValue(task.Id);
        cmd.Parameters.AddWithValue(TenantContext.Normalize(task.TenantId));
        cmd.Parameters.AddWithValue(task.InstanceId);
        cmd.Parameters.AddWithValue(task.ParentInstanceId);
        cmd.Parameters.AddWithValue(task.DefinitionKey);
        cmd.Parameters.AddWithValue(task.Title);
        cmd.Parameters.AddWithValue(task.Note);
        cmd.Parameters.AddWithValue(task.AssigneeKind);
        cmd.Parameters.AddWithValue(task.AssigneeId);
        cmd.Parameters.AddWithValue(task.AssignedBy);
        cmd.Parameters.AddWithValue(task.ClaimedBy);
        cmd.Parameters.AddWithValue(task.Status);
        cmd.Parameters.AddWithValue(task.CreatedAt);
        cmd.Parameters.AddWithValue(task.UpdatedAt);
        cmd.Parameters.AddWithValue(task.CompletedAt is { } c ? c : DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<WorkflowTask> GetTask(string id, CancellationToken cancellationToken = default)
    {
        await using var cmd = _ds.CreateCommand(TaskSelect + " WHERE id=$1");
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw EngineException.NotFound();
        return ScanTask(reader);
    }

    public async Task<WorkflowTask> TransitionTask(string id, IReadOnlyList<string> allowed, Action<WorkflowTask> apply, CancellationToken cancellationToken = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        WorkflowTask task;
        await using (var cmd = new NpgsqlCommand(TaskSelect + " WHERE id=$1 FOR UPDATE", conn, tx))
        {
            cmd.Parameters.AddWithValue(id);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw EngineException.NotFound();
            task = ScanTask(reader);
        }
        if (!allowed.Contains(task.Status))
            throw EngineException.NotOpen();
        apply(task);
        await using (var cmd = new NpgsqlCommand("""
            UPDATE tasks SET status=$2, note=$3, updated_at=$4, completed_at=$5, claimed_by=$6 WHERE id=$1
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(task.Id);
            cmd.Parameters.AddWithValue(task.Status);
            cmd.Parameters.AddWithValue(task.Note);
            cmd.Parameters.AddWithValue(task.UpdatedAt);
            cmd.Parameters.AddWithValue(task.CompletedAt is { } c ? c : DBNull.Value);
            cmd.Parameters.AddWithValue(task.ClaimedBy);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        await tx.CommitAsync(cancellationToken);
        return task;
    }

    private const string TaskSelect = """
        SELECT id, tenant_id, instance_id, parent_instance_id, definition_key, title, note,
          assignee_kind, assignee_id, assigned_by, claimed_by, status, created_at, updated_at, completed_at
        FROM tasks
        """;

    private static WorkflowTask ScanTask(NpgsqlDataReader row)
    {
        DateTime? completed = row.IsDBNull(14) ? null : Utc(row.GetDateTime(14));
        return new WorkflowTask
        {
            Id = row.GetString(0),
            TenantId = row.GetString(1),
            InstanceId = row.GetString(2),
            ParentInstanceId = row.GetString(3),
            DefinitionKey = row.GetString(4),
            Title = row.GetString(5),
            Note = row.GetString(6),
            AssigneeKind = row.GetString(7),
            AssigneeId = row.GetString(8),
            AssignedBy = row.GetString(9),
            ClaimedBy = row.GetString(10),
            Status = row.GetString(11),
            CreatedAt = Utc(row.GetDateTime(12)),
            UpdatedAt = Utc(row.GetDateTime(13)),
            CompletedAt = completed,
        };
    }

    public async Task<IReadOnlyList<WorkflowTask>> ListTasks(TaskFilter f, CancellationToken cancellationToken = default)
    {
        var q = TaskSelect + " WHERE 1=1";
        var args = new List<object>();
        var i = 1;
        if (!string.IsNullOrEmpty(f.TenantId))
        {
            q += $" AND tenant_id=${i}";
            args.Add(TenantContext.Normalize(f.TenantId));
            i++;
        }
        if (!string.IsNullOrEmpty(f.InstanceId))
        {
            q += $" AND (instance_id=${i} OR parent_instance_id=${i})";
            args.Add(f.InstanceId);
            i++;
        }
        if (!string.IsNullOrEmpty(f.Status))
        {
            q += $" AND status=${i}";
            args.Add(f.Status);
            i++;
        }
        if (f.Statuses is { Count: > 0 })
        {
            q += $" AND status = ANY(${i})";
            args.Add(f.Statuses.ToArray());
            i++;
        }
        if (!string.IsNullOrEmpty(f.ClaimedBy))
        {
            q += $" AND claimed_by=${i}";
            args.Add(f.ClaimedBy);
            i++;
        }
        if (!string.IsNullOrEmpty(f.GroupId))
        {
            q += $" AND assignee_kind='group' AND assignee_id=${i}";
            args.Add(f.GroupId);
        }
        else if (!string.IsNullOrEmpty(f.UserId))
        {
            if (f.GroupIds is null || f.GroupIds.Count == 0)
            {
                q += $" AND assignee_kind='user' AND assignee_id=${i}";
                args.Add(f.UserId);
            }
            else
            {
                q += $" AND ((assignee_kind='user' AND assignee_id=${i}) OR (assignee_kind='group' AND assignee_id = ANY(${i + 1})))";
                args.Add(f.UserId);
                args.Add(f.GroupIds.ToArray());
            }
        }

        await using var cmd = _ds.CreateCommand(q + " ORDER BY created_at");
        foreach (var a in args)
            cmd.Parameters.AddWithValue(a);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var outList = new List<WorkflowTask>();
        while (await reader.ReadAsync(cancellationToken))
            outList.Add(ScanTask(reader));
        return outList;
    }

    private static DateTime Utc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
}
