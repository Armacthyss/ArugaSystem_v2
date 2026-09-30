using AndroidWebAPI.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AndroidWebAPI.Data
{
    public class VaccineInventoryRepository
    {
        private readonly string _connectionString;

        public VaccineInventoryRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        // ========================================
        // GET ALL
        // ========================================
        public async Task<IEnumerable<VaccineInventory>> GetAllAsync()
        {
            using IDbConnection connection = new SqlConnection(_connectionString);

            string sql = @"
            SELECT *
                FROM VaccineInventory
                ORDER BY ExpirationDate
                ";
                
            return await connection.QueryAsync<VaccineInventory>(sql);
        }

        // ========================================
        // GET BY ID
        // ========================================
        public async Task<VaccineInventory?> GetByIdAsync(int inventoryId)
        {
            using IDbConnection connection = new SqlConnection(_connectionString);

            string sql = @"
                SELECT *
                FROM VaccineInventory
                WHERE InventoryID = @InventoryID";

            return await connection.QueryFirstOrDefaultAsync<VaccineInventory>(
                sql,
                new { InventoryID = inventoryId });
        }

        // ========================================
        // GET BY VACCINE
        // ========================================
        public async Task<IEnumerable<VaccineInventory>> GetByVaccineIdAsync(int vaccineId)
        {
            using IDbConnection connection = new SqlConnection(_connectionString);

            string sql = @"
                SELECT *
                FROM VaccineInventory
                WHERE VaccineID = @VaccineID
                ORDER BY ExpirationDate";

            return await connection.QueryAsync<VaccineInventory>(
                sql,
                new { VaccineID = vaccineId });
        }

        // ========================================
        // CREATE
        // ========================================
        public async Task<VaccineInventory> CreateAsync(VaccineInventory inventory)
        {
            using IDbConnection connection = new SqlConnection(_connectionString);

            string sql = @"
                INSERT INTO VaccineInventory
                (
                    VaccineID,
                    LotNumber,
                    InitialQuantity,
                    CurrentQuantity,
                    MinimumStock,
                    ManufacturingDate,
                    ExpirationDate,
                    ReceivedDate,
                    Supplier,
                    Status,
                    CreatedAt
                )
                VALUES
                (
                    @VaccineID,
                    @LotNumber,
                    @InitialQuantity,
                    @CurrentQuantity,
                    @MinimumStock,
                    @ManufacturingDate,
                    @ExpirationDate,
                    @ReceivedDate,
                    @Supplier,
                    @Status,
                    GETDATE()
                );

                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            inventory.InventoryID =
                await connection.QuerySingleAsync<int>(sql, inventory);

            return inventory;
        }

        // ========================================
        // UPDATE
        // ========================================
        public async Task<VaccineInventory> UpdateAsync(VaccineInventory inventory)
        {
            using IDbConnection connection = new SqlConnection(_connectionString);

            string sql = @"
                UPDATE VaccineInventory
                SET
                    VaccineID = @VaccineID,
                    LotNumber = @LotNumber,
                    InitialQuantity = @InitialQuantity,
                    CurrentQuantity = @CurrentQuantity,
                    MinimumStock = @MinimumStock,
                    ManufacturingDate = COALESCE(@ManufacturingDate, ManufacturingDate),  -- screens that don't show it keep the saved one
                    ExpirationDate = @ExpirationDate,
                    ReceivedDate = @ReceivedDate,
                    Supplier = @Supplier,
                    Status = @Status,
                    UpdatedAt = GETDATE()
                WHERE InventoryID = @InventoryID";

            await connection.ExecuteAsync(sql, inventory);

            return inventory;
        }

        // ========================================
        // DELETE
        // ========================================
        public async Task<bool> DeleteAsync(int inventoryId)
        {
            using IDbConnection connection = new SqlConnection(_connectionString);

            string sql = @"
                DELETE FROM VaccineInventory
                WHERE InventoryID = @InventoryID";

            int rows = await connection.ExecuteAsync(sql,
                new { InventoryID = inventoryId });

            return rows > 0;
        }
    }
}