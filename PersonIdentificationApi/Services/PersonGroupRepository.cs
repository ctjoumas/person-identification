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

        public async Task<int> InsertPersonGroupAsync(IDbConnection dbConnection, DbPersonGroup personGroup, IDbTransaction transaction)
        {
            string query = @"
                INSERT INTO PersonGroup (PersonGroupId, PersonGroupName, IsTrained)
                VALUES (@PersonGroupId, @PersonGroupName, @IsTrained)";

            return await dbConnection.ExecuteAsync(query, personGroup, transaction);
        }

        public async Task<int> InsertPersonGroupImageAsync(IDbConnection dbConnection, DbPersonGroupImage personGroupImage, IDbTransaction transaction)
        {
            string query = @"
                INSERT INTO PersonGroupImage (PersonId, PersonGroupId, BlobName, BlobUrl)
                VALUES (@PersonId, @PersonGroupId, @BlobName, @BlobUrl)";

            return await dbConnection.ExecuteAsync(query, personGroupImage, transaction);
        }

        public async Task<int> InsertPersonFaceAsync(IDbConnection dbConnection, DbPersonFace personFace, IDbTransaction transaction)
        {
            string query = @"
                INSERT INTO PersonFace (FaceId, PersonId)
                VALUES (@FaceId, @PersonId)";

            return await dbConnection.ExecuteAsync(query, personFace, transaction);
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
                JOIN [dbo].[PersonGroupImage] pgi on pg.PersonGroupId = pgi.PersonGroupId
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
            using (IDbConnection dbConnection = new SqlConnection(_connectionString))
            {
                dbConnection.Open();
                using (IDbTransaction transaction = dbConnection.BeginTransaction())
                {
                    try
                    {
                        await InsertPersonGroupAsync(dbConnection, dbPersonGroup, transaction);

                        foreach (var dbPersonGroupImage in dbPersonGroupImages)
                        {
                            await InsertPersonGroupImageAsync(dbConnection, dbPersonGroupImage, transaction);
                        }

                        foreach (var dbPersonFace in dbPersonFaces)
                        {
                            await InsertPersonFaceAsync(dbConnection, dbPersonFace, transaction);
                        }

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}