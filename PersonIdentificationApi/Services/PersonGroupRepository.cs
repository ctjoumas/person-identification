using Dapper;
using PersonIdentificationApi.Models;
using System.Data;
using System.Data.SqlClient;

namespace PersonIdentificationApi.Services
{
    public class PersonGroupRepository
    {
        private readonly string _connectionString;

        public PersonGroupRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<int> InsertPersonGroupAsync(DbPersonGroup personGroup)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                string query = @"
                INSERT INTO PersonGroup (PersonGroupId, IsTrained, IsDeleted, CreatedBy)
                VALUES (@PersonGroupId, @IsTrained, @IsDeleted, @CreatedBy)";
                return await db.ExecuteAsync(query, personGroup);
            }
        }

        public async Task<int> InsertPersonGroupImageAsync(DbPersonGroupImage personGroupImage)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                string query = @"
                INSERT INTO PersonGroupImages (PersonGroupImageId, PersonGroupId, PersonId, BlobName, BlobUrl, CreatedBy)
                VALUES (@PersonGroupImageId, @PersonGroupId, @PersonId, @BlobName, @BlobUrl, @CreatedBy)";
                return await db.ExecuteAsync(query, personGroupImage);
            }
        }
    }
}
