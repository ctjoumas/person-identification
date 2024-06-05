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
            using IDbConnection db = new SqlConnection(_connectionString);
            string query = @"
                INSERT INTO PersonGroup (PersonGroupId, IsTrained, IsDeleted, CreatedBy)
                VALUES (@PersonGroupId, @IsTrained, @IsDeleted, @CreatedBy)";
            return await db.ExecuteAsync(query, personGroup);
        }

        public async Task<int> InsertPersonGroupImageAsync(DbPersonGroupImage personGroupImage)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string query = @"
                INSERT INTO PersonGroupImages (PersonGroupImageId, PersonGroupId, BlobName, BlobUrl, CreatedBy)
                VALUES (@PersonGroupImageId, @PersonGroupId, @BlobName, @BlobUrl, @CreatedBy)";
            return await db.ExecuteAsync(query, personGroupImage);
        }

        // Gets all person groups that have been trained.
        public async Task<List<DbPersonGroupImage>> GetPersonGroupAsync(Guid personGroupId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            string query = @"
                SELECT 
                pg.[PersonGroupId],
                pgi.BlobName,
                pgi.BlobUrl
                FROM [dbo].[PersonGroup] pg
                JOIN [dbo].[PersonGroupImages] pgi on pg.PersonGroupId = pgi.PersonGroupId
                WHERE pg.PersonGroupId = @PersonGroupId";

            var personGroupImages = await db.QueryAsync<DbPersonGroupImage>(query);

            return personGroupImages.ToList();
        }

        // Gets all person groups that have been trained.
        public async Task<List<DbPersonGroupImage>> GetPersonGroupsAsync()
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            string query = @"
                SELECT 
                pg.[PersonGroupId],
                pgi.BlobName,
                pgi.BlobUrl
                FROM [dbo].[PersonGroup] pg
                JOIN [dbo].[PersonGroupImages] pgi on pg.PersonGroupId = pgi.PersonGroupId
                WHERE pg.IsTrained = 1";

            var personGroupImages = await db.QueryAsync<DbPersonGroupImage>(query);

            return personGroupImages.ToList();
        }

        public async Task DeletePersonGroupAsync(Guid personGroupId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            string query = @"
                UPDATE PersonGroup
                SET IsDeleted = 1,
                ModifiedBy = 'system',
                ModifiedDate = getutcdate()
                WHERE PersonGroupId = @PersonGroupId";

            await db.ExecuteAsync(query, new { PersonGroupId = personGroupId });
        }
    }
}