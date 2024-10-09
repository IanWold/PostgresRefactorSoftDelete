namespace PostgresRefactorSoftDelete;

public class SeparateTableMigration : IMigration
{
    public static string Step1 => """
        CREATE TABLE items_deleted (
            deleted TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            LIKE items INCLUDING ALL
        );

        CREATE TABLE carts_deleted (
            deleted TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            LIKE carts INCLUDING ALL
        );

        CREATE TABLE cart_items_deleted (
            deleted TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            LIKE cart_items
        );
        """;

    public static string Step2 => """
        CREATE VIEW items_combined AS SELECT null AS deleted, * FROM items UNION ALL SELECT * FROM items_deleted;
        CREATE VIEW carts_combined AS SELECT null AS deleted, * FROM carts UNION ALL SELECT * FROM carts_deleted;
        CREATE VIEW cart_items_combined AS SELECT null AS deleted, * FROM cart_items UNION ALL SELECT * FROM cart_items_deleted;
        """;
        
    public static string Step3 => """
        CREATE OR REPLACE FUNCTION items_soft_delete() RETURNS TRIGGER AS $$
        BEGIN
            INSERT INTO items_deleted (item_id, name, created)
            VALUES (OLD.item_id, OLD.name, OLD.created);
            RETURN OLD;
        END;
        $$ LANGUAGE plpgsql;
        CREATE TRIGGER trigger_items_delete BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION items_soft_delete();

        CREATE OR REPLACE FUNCTION carts_soft_delete() RETURNS TRIGGER AS $$
        BEGIN
            INSERT INTO carts_deleted (cart_id, created)
            VALUES (OLD.cart_id, OLD.created);
            RETURN OLD;
        END;
        $$ LANGUAGE plpgsql;
        CREATE TRIGGER trigger_carts_delete BEFORE DELETE ON carts FOR EACH ROW EXECUTE FUNCTION carts_soft_delete();

        CREATE OR REPLACE FUNCTION cart_items_soft_delete() RETURNS TRIGGER AS $$
        BEGIN
            INSERT INTO cart_items_deleted (cart_item_id, cart_id, item_id, created)
            VALUES (OLD.cart_item_id, OLD.cart_id, OLD.item_id, OLD.created);
            RETURN OLD;
        END;
        $$ LANGUAGE plpgsql;
        CREATE TRIGGER trigger_cart_items_delete BEFORE DELETE ON cart_items FOR EACH ROW EXECUTE FUNCTION cart_items_soft_delete();
        """;
        
    public static string SelectDeletedItemIds => """
        SELECT item_id FROM items_deleted
        """;
        
    public static string SelectDeletedCartIds => """
        SELECT cart_id FROM carts_deleted
        """;
        
    public static string SelectDeletedCartItemIds => """
        SELECT cart_item_id FROM cart_items_deleted
        """;
}