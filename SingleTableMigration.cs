namespace PostgresRefactorSoftDelete;

public class SingleTableMigration : IMigration
{
    public static string Step1 => """
        ALTER TABLE items ADD COLUMN deleted TIMESTAMP DEFAULT NULL;
        ALTER TABLE carts ADD COLUMN deleted TIMESTAMP DEFAULT NULL;
        ALTER TABLE cart_items ADD COLUMN deleted TIMESTAMP DEFAULT NULL;
        """;

    public static string Step2 => """
        ALTER TABLE items RENAME TO items_all;
        CREATE VIEW items AS SELECT * FROM items_all WHERE deleted IS NULL;

        ALTER TABLE carts RENAME TO carts_all;
        CREATE VIEW carts AS SELECT * FROM carts_all WHERE deleted IS NULL;

        ALTER TABLE cart_items RENAME TO cart_items_all;
        CREATE VIEW cart_items AS SELECT * FROM cart_items_all WHERE deleted IS NULL;
        """;
        
    public static string Step3 => """
        CREATE RULE rule_soft_delete AS ON DELETE TO items DO INSTEAD (UPDATE items_all SET deleted = CURRENT_TIMESTAMP WHERE item_id = OLD.item_id);
        CREATE RULE rule_soft_delete AS ON DELETE TO carts DO INSTEAD (UPDATE carts_all SET deleted = CURRENT_TIMESTAMP WHERE cart_id = OLD.cart_id);
        CREATE RULE rule_soft_delete AS ON DELETE TO cart_items DO INSTEAD (UPDATE cart_items_all SET deleted = CURRENT_TIMESTAMP WHERE cart_item_id = OLD.cart_item_id);
        
        CREATE RULE rule_cascade_deleted_cart_items AS ON UPDATE TO carts_all
            WHERE OLD.deleted IS DISTINCT FROM NEW.deleted
            DO ALSO UPDATE cart_items_all SET deleted = NEW.deleted WHERE cart_id = OLD.cart_id;

        CREATE RULE rule_cascade_deleted_cart_items AS ON UPDATE TO items_all
            WHERE OLD.deleted IS DISTINCT FROM NEW.deleted
            DO ALSO UPDATE cart_items_all SET deleted = NEW.deleted WHERE item_id = OLD.item_id;
        """;
        
    public static string SelectDeletedItemIds => """
        SELECT item_id FROM items_all WHERE deleted IS NOT NULL
        """;
        
    public static string SelectDeletedCartIds => """
        SELECT cart__id FROM carts_all WHERE deleted IS NOT NULL
        """;
        
    public static string SelectDeletedCartItemIds => """
        SELECT cart_item_id FROM cart_items_all WHERE deleted IS NOT NULL
        """;
}
