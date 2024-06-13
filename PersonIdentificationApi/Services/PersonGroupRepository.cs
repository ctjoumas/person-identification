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
                INSERT INTO PersonGroup (PersonGroupId, PersonGroupName)
                VALUES (@PersonGroupId, @PersonGroupName)";

            return await dbConnection.ExecuteAsync(query, personGroup, transaction);
        }

        public async Task<int> InsertPersonGroupPersonsync(IDbConnection dbConnection, DbPersonGroupPerson personGroupImage, IDbTransaction transaction)
        {
            string query = @"
                INSERT INTO PersonGroupPerson (PersonId, PersonGroupId, PersonName)
                VALUES (@PersonId, @PersonGroupId, @PersonName)";

            return await dbConnection.ExecuteAsync(query, personGroupImage, transaction);
        }

        public async Task<int> InsertPersonGroupPersonFaceAsync(IDbConnection dbConnection, DbPersonGroupPersonFace personFace, IDbTransaction transaction)
        {
            string query = @"
                INSERT INTO PersonGroupPersonFace (FaceId, PersonId, BlobName, BlobUrl, IsTrained)
                VALUES (@FaceId, @PersonId, @BlobName, @BlobUrl, @IsTrained)";

            return await dbConnection.ExecuteAsync(query, personFace, transaction);
        }

        // Gets all person groups that have been trained.
        public async Task<List<DbDetectionResult>> GetPersonGroupsAsync()
        {
            using IDbConnection dbConnection = new SqlConnection(_connectionString);

            string query = @"
                SELECT 
                pg.[PersonGroupId],
                pg.[PersonGroupName],
                pgp.PersonName                
                FROM [dbo].[PersonGroup] pg
                JOIN [dbo].[PersonGroupPerson] pgp on pg.PersonGroupId = pgp.PersonGroupId
                WHERE pg.IsTrained = 1";

            var dbDetectionResults = await dbConnection.QueryAsync<DbDetectionResult>(query);

            return dbDetectionResults.ToList();
        }

        public async Task<DbDetectionResult> GetPersonGroupAsync(string personGroupId, string personName)
        {
            using IDbConnection dbConnection = new SqlConnection(_connectionString);

            string query = @"
                SELECT 
                pg.[PersonGroupId],
                pg.[PersonGroupName],
                pgp.PersonName,
                pgp.PersonId
                FROM [dbo].[PersonGroup] pg
                JOIN [dbo].[PersonGroupPerson] pgp on pg.PersonGroupId = pgp.PersonGroupId
                JOIN [dbo].[PersonGroupPersonFace] f on pgp.PersonId = f.PersonId
                WHERE f.IsTrained = 1 AND pg.PersonGroupId = @PersonGroupId AND pgp.PersonName = @PersonName";

            var dbDetectionResults = await dbConnection.QueryFirstOrDefaultAsync<DbDetectionResult>(query, new { PersonGroupId = personGroupId, PersonName = personName });

            return dbDetectionResults;
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

        public async Task CreatePersonGroupAllAsync(
            DbPersonGroup dbPersonGroup,
            List<DbPersonGroupPerson> dbPersonGroupPeople,
            List<DbPersonGroupPersonFace> dbPersonFaces)
        {
            using IDbConnection dbConnection = new SqlConnection(_connectionString);
            dbConnection.Open();
            using IDbTransaction transaction = dbConnection.BeginTransaction();

            try
            {
                await InsertPersonGroupAsync(dbConnection, dbPersonGroup, transaction);

                foreach (var dbPersonGroupImage in dbPersonGroupPeople)
                {
                    await InsertPersonGroupPersonsync(dbConnection, dbPersonGroupImage, transaction);
                }

                foreach (var dbPersonFace in dbPersonFaces)
                {
                    await InsertPersonGroupPersonFaceAsync(dbConnection, dbPersonFace, transaction);
                }

                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task AddFacesToExistingPerson(string personGroupId, List<DbPersonGroupPerson> dbPersonGroupPeople, List<DbPersonGroupPersonFace> dbPersonFaces)
        {
            using IDbConnection dbConnection = new SqlConnection(_connectionString);
            dbConnection.Open();
            using IDbTransaction transaction = dbConnection.BeginTransaction();

            try
            {
                // TODO: This could be refactored along with passing in an actual user.
                string query = @"
                UPDATE PersonGroup
                SET ModifiedDate = getutcdate(),
                    ModifiedBy = 'system'
                WHERE PersonGroupId = @PersonGroupId";
  
                await dbConnection.ExecuteAsync(query, new { PersonGroupId = personGroupId }, transaction);

                if (dbPersonGroupPeople.Any())
                {
                    foreach (var dbPersonGroupImage in dbPersonGroupPeople)
                    {
                        await InsertPersonGroupPersonsync(dbConnection, dbPersonGroupImage, transaction);
                    }
                }

                foreach (var dbPersonFace in dbPersonFaces)
                {
                    await InsertPersonGroupPersonFaceAsync(dbConnection, dbPersonFace, transaction);
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