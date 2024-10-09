using Npgsql;

namespace PostgresRefactorSoftDelete;

public class DatabaseExecutor(string connectionString)
{
    private static NpgsqlCommand GetCommand(string query, NpgsqlConnection connection, NpgsqlTransaction? transaction = null)
    {
        var command = connection.CreateCommand();

        command.Connection = connection;
        command.CommandText = query;

        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        return command;
    }

    public void Command(string query)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = GetCommand(query, connection, transaction);
        command.ExecuteNonQuery();

        transaction.Commit();

        connection.Close();
    }

    public int CommandInsert(string query)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = GetCommand(query, connection, transaction);
        var reader = command.ExecuteReader();
        reader.Read();

        var id = reader.GetInt32(0);

        reader.Close();

        transaction.Commit();
        connection.Close();

        return id;
    }
    
    public IEnumerable<int> QueryIds(string query)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        var command = GetCommand(query, connection);
        var reader = command.ExecuteReader();

        var ids = new List<int>();
        while (reader.Read())
        {
            ids.Add(reader.GetInt32(0));
        }

        reader.Close();
        connection.Close();

        return ids;
    }
}
