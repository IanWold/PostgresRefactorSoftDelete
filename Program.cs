using PostgresRefactorSoftDelete;

const string singleTableConnectionString = "";
const string separateTableConnectionString = "";

const bool clearDatabaseBeforeTests = false;

#region Queries

const string clearDatabaseQuery = """
    DROP SCHEMA public CASCADE;
    CREATE SCHEMA public;
    GRANT ALL ON SCHEMA public TO postgres;
    GRANT ALL ON SCHEMA public TO public;
    """;

const string setupDatabaseQuery = """
    CREATE TABLE items (
        item_id SERIAL PRIMARY KEY,
        name VARCHAR(255) NOT NULL,
        created TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    );

    CREATE TABLE carts (
        cart_id SERIAL PRIMARY KEY,
        created TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    );

    CREATE TABLE cart_items (
        cart_item_id SERIAL PRIMARY KEY,
        cart_id INT NOT NULL,
        item_id INT NOT NULL,
        created TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

        CONSTRAINT fk_cart FOREIGN KEY (cart_id) REFERENCES carts (cart_id) ON DELETE CASCADE,
        CONSTRAINT fk_item FOREIGN KEY (item_id) REFERENCES items (item_id) ON DELETE CASCADE
    );
    """;

const string getAllItemIdsQuery = """
    SELECT item_id FROM items
    """;

const string getAllCartIdsQuery = """
    SELECT cart_id FROM carts
    """;

const string getAllCartItemIdsQuery = """
    SELECT cart_item_id FROM cart_items
    """;

#endregion

if (!string.IsNullOrEmpty(singleTableConnectionString))
{
    ExecuteTest<SingleTableMigration>("Single Table", new(singleTableConnectionString));
}

if (!string.IsNullOrEmpty(separateTableConnectionString))
{
    ExecuteTest<SeparateTableMigration>("Separate Table", new(separateTableConnectionString));
}

#region Test

void ExecuteTest<TMigration>(string testName, DatabaseExecutor executor) where TMigration : IMigration
{
    Console.WriteLine($"{testName}: Setting up");

    if (clearDatabaseBeforeTests)
    {
        executor.Command(clearDatabaseQuery);
    }

    executor.Command(setupDatabaseQuery);

    var addedRecords = new RecordsToMatch([], [], []);
    var deletedRecords = new RecordsToMatch([], [], []);

    // Step 1

    (addedRecords, deletedRecords) = PerformUserActions(testName, executor, addedRecords, deletedRecords);
    
    Console.WriteLine($"{testName}: Performing migration step 1");
    executor.Command(TMigration.Step1);

    // Step 2

    (addedRecords, deletedRecords) = PerformUserActions(testName, executor, addedRecords, deletedRecords);
    
    Console.WriteLine($"{testName}: Performing migration step 2");
    executor.Command(TMigration.Step2);

    // Step 3

    (addedRecords, deletedRecords) = PerformUserActions(testName, executor, addedRecords, deletedRecords);
    
    Console.WriteLine($"{testName}: Performing migration step 3");
    executor.Command(TMigration.Step3);

    // Post-Migration
    
    Console.WriteLine($"{testName}: Finished migrating");

    (addedRecords, var softDeletedRecords) = PerformUserActions(testName, executor, addedRecords, new([], [], []));

    var isAddedDeletedValid = MatchRecords(executor, addedRecords, deletedRecords, getAllItemIdsQuery, getAllCartIdsQuery, getAllCartItemIdsQuery);
    Console.WriteLine($"{testName}: Check if added/deleted records are correct: {isAddedDeletedValid}");

    var isSoftDeletedValid = MatchRecords(executor, softDeletedRecords, addedRecords, TMigration.SelectDeletedItemIds, TMigration.SelectDeletedCartIds, TMigration.SelectDeletedCartItemIds);
    Console.WriteLine($"{testName}: Check if soft deleted records are correct: {isSoftDeletedValid}");
}

(RecordsToMatch added, RecordsToMatch deleted) PerformUserActions(string testName, DatabaseExecutor executor, RecordsToMatch accumulateAddedRecords, RecordsToMatch accumulateDeletedRecords)
{
    Console.WriteLine($"{testName}: Performing user actions");

    var firstItemName = Guid.NewGuid().ToString();
    var firstItemId = executor.CommandInsert($"INSERT INTO items (name) VALUES ('{firstItemName}') RETURNING item_id");

    var secondItemName = Guid.NewGuid().ToString();
    var secondItemId = executor.CommandInsert($"INSERT INTO items (name) VALUES ('{secondItemName}') RETURNING item_id");

    var thirdItemName = Guid.NewGuid().ToString();
    var thirdItemId = executor.CommandInsert($"INSERT INTO items (name) VALUES ('{thirdItemName}') RETURNING item_id");

    var firstCartId = executor.CommandInsert("INSERT INTO carts DEFAULT VALUES RETURNING cart_id");
    var secondCartId = executor.CommandInsert("INSERT INTO carts DEFAULT VALUES RETURNING cart_id");

    var firstCartFirstItemId = executor.CommandInsert($"INSERT INTO cart_items (cart_id, item_id) VALUES ({firstCartId}, {firstItemId}) RETURNING cart_item_id");
    var firstCartSecondItemId = executor.CommandInsert($"INSERT INTO cart_items (cart_id, item_id) VALUES ({firstCartId}, {secondItemId}) RETURNING cart_item_id");
    var firstCartThirdItemId = executor.CommandInsert($"INSERT INTO cart_items (cart_id, item_id) VALUES ({firstCartId}, {thirdItemId}) RETURNING cart_item_id");
    
    var secondCartFirstItemId = executor.CommandInsert($"INSERT INTO cart_items (cart_id, item_id) VALUES ({secondCartId}, {firstItemId}) RETURNING cart_item_id");
    var secondCartSecondItemId = executor.CommandInsert($"INSERT INTO cart_items (cart_id, item_id) VALUES ({secondCartId}, {secondItemId}) RETURNING cart_item_id");
    var secondCartThirdItemId = executor.CommandInsert($"INSERT INTO cart_items (cart_id, item_id) VALUES ({secondCartId}, {thirdItemId}) RETURNING cart_item_id");

    executor.Command($"DELETE FROM items WHERE item_id = {thirdItemId}");
    executor.Command($"DELETE FROM carts WHERE cart_id = {secondCartId}");

    accumulateAddedRecords = accumulateAddedRecords with
    {
        ItemIds = [..accumulateAddedRecords.ItemIds, firstItemId, secondItemId],
        CartIds = [..accumulateAddedRecords.CartIds, firstCartId],
        CartItemIds = [..accumulateAddedRecords.CartItemIds, firstCartFirstItemId, firstCartSecondItemId]
    };

    foreach (var itemId in accumulateAddedRecords.ItemIds)
    {
        var newItemName = Guid.NewGuid().ToString();
        executor.Command($"UPDATE items SET name = '{newItemName}' WHERE item_id = {itemId}");
    }

    accumulateDeletedRecords = accumulateDeletedRecords with
    {
        ItemIds = [..accumulateDeletedRecords.ItemIds, thirdItemId],
        CartIds = [..accumulateDeletedRecords.CartIds, secondCartId],
        CartItemIds = [..accumulateDeletedRecords.CartItemIds, firstCartThirdItemId, secondCartFirstItemId, secondCartSecondItemId, secondCartThirdItemId]
    };
    
    var isValid = MatchRecords(executor, accumulateDeletedRecords, accumulateDeletedRecords, getAllItemIdsQuery, getAllCartIdsQuery, getAllCartItemIdsQuery);
    Console.WriteLine($"{testName}: Check if added/deleted records are correct: {isValid}");

    return (accumulateAddedRecords, accumulateDeletedRecords);
}

bool MatchRecords(DatabaseExecutor executor, RecordsToMatch expected, RecordsToMatch notExpected, string itemsQuery, string cartsQuery, string cartItemsQuery)
{
    var itemIds = executor.QueryIds(itemsQuery);
    var cartIds = executor.QueryIds(cartsQuery);
    var cartItemIds = executor.QueryIds(cartItemsQuery);

    return
        expected.ItemIds.All(itemIds.Contains)
        && expected.CartIds.All(cartIds.Contains)
        && expected.CartItemIds.All(cartItemIds.Contains)
        && !notExpected.ItemIds.Any(itemIds.Contains)
        && !notExpected.CartIds.Any(cartIds.Contains)
        && !notExpected.CartItemIds.Any(cartItemIds.Contains);
}

#endregion

record RecordsToMatch(
    IEnumerable<int> ItemIds,
    IEnumerable<int> CartIds,
    IEnumerable<int> CartItemIds
);
