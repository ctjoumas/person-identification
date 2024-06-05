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

        public async Task<int> InsertPersonGroupAsync(IDbConnection dbConnection, DbPersonGroup personGroup)
        {
            string query = @"
                INSERT INTO PersonGroup (PersonGroupId, IsTrained, CreatedBy)
                VALUES (@PersonGroupId, @IsTrained, @CreatedBy)";

            return await dbConnection.ExecuteAsync(query, personGroup);
        }

        public async Task<int> InsertPersonGroupImageAsync(IDbConnection dbConnection, DbPersonGroupImage personGroupImage)
        {
            string query = @"
                INSERT INTO PersonGroupImage (PersonId, PersonGroupId, BlobName, BlobUrl, CreatedBy)
                VALUES (@PersonId, @BlobName, @BlobUrl, @CreatedBy)";

            return await dbConnection.ExecuteAsync(query, personGroupImage);
        }

        public async Task<int> InsertPersonFaceAsync(IDbConnection dbConnection, DbPersonFace personFace)
        {
            string query = @"
                INSERT INTO PersonFace (FaceId, PersonId, CreatedBy)
                VALUES (@FaceId, @PersonId, @CreatedBy)";

            return await dbConnection.ExecuteAsync(query, personFace);
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
            using IDbConnection dbConnection = new SqlConnection(_connectionString);

            string query = @"
                SELECT 
                pg.[PersonGroupId],
                pgi.BlobName,
                pgi.BlobUrl
                FROM [dbo].[PersonGroup] pg
                JOIN [dbo].[PersonGroupImages] pgi on pg.PersonGroupId = pgi.PersonGroupId
                WHERE pg.IsTrained = 1";

            var personGroupImages = await dbConnection.QueryAsync<DbPersonGroupImage>(query);

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

        public async Task SavePersonGroupAllAsync(
            DbPersonGroup dbPersonGroup,
            List<DbPersonGroupImage> dbPersonGroupImages,
            List<DbPersonFace> dbPersonFaces)
        {
            using IDbConnection dbConnection = new SqlConnection(_connectionString);
            dbConnection.Open();
            using IDbTransaction transaction = dbConnection.BeginTransaction();

            try
            {
                // Execute your methods here, passing the transaction object
                await InsertPersonGroupAsync(dbConnection, dbPersonGroup);

                foreach(var dbPersonGroupImage in dbPersonGroupImages)
                {
                    await InsertPersonGroupImageAsync(dbConnection, dbPersonGroupImage);
                }

                foreach(var dbPersonFace in dbPersonFaces)
                {
                    await InsertPersonFaceAsync(dbConnection, dbPersonFace);
                }

                transaction.Commit();
            }
            catch (Exception)
            {
                // Rollback the transaction in case of any failure
                transaction.Rollback();
                throw;
            }
        }
    }
}