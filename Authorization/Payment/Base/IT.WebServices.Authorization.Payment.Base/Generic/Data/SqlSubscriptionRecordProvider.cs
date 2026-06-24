using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using MySql.Data.MySqlClient;

namespace IT.WebServices.Authorization.Payment.Generic.Data
{
    internal class SqlSubscriptionRecordProvider : IGenericSubscriptionRecordProvider
    {
        public readonly MySQLHelper sql;

        public SqlSubscriptionRecordProvider(MySQLHelper sql)
        {
            this.sql = sql;
        }

        public async Task Delete(Guid userId, Guid subId)
        {
            try
            {
                const string query = @"
                    DELETE FROM
                        Payment_Generic_Subscription
                    WHERE
                        UserID = @UserID
                        AND InternalSubscriptionID = @InternalSubscriptionID;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserID", userId.ToString()),
                    new MySqlParameter("InternalSubscriptionID", subId.ToString()),
                };

                await sql.RunCmd(query, parameters);
            }
            catch (Exception)
            {
            }
        }

        public async Task<bool> Exists(Guid userId, Guid subId)
        {
            var rec = await GetById(userId, subId);
            return rec != null;
        }

        public async IAsyncEnumerable<GenericSubscriptionRecord> GetAll()
        {
            const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Generic_Subscription
                ";

            using var rdr = await sql.ReturnReader(query);

            while (await rdr.ReadAsync())
            {
                var record = rdr.ParseSubscriptionRecord();

                if (record != null)
                    yield return record;
            }
        }

        public async IAsyncEnumerable<GenericSubscriptionRecord> GetAllByUserId(Guid userId)
        {
            const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Generic_Subscription
                    WHERE
                        UserID = @UserID;
                ";

            var parameters = new MySqlParameter[]
            {
                    new MySqlParameter("UserID", userId.ToString())
            };

            using var rdr = await sql.ReturnReader(query, parameters);

            while (await rdr.ReadAsync())
            {
                var record = rdr.ParseSubscriptionRecord();

                if (record != null)
                    yield return record;
            }
        }

        public async IAsyncEnumerable<(Guid userId, Guid subId)> GetAllSubscriptionIds()
        {
            const string query = @"
                    SELECT
                        UserID,
                        InternalSubscriptionID
                    FROM
                        Payment_Generic_Subscription
                ";

            using var rdr = await sql.ReturnReader(query);

            while (await rdr.ReadAsync())
            {
                var userId = (rdr["UserID"] as string ?? "").ToGuid();
                var subId = (rdr["InternalSubscriptionID"] as string ?? "").ToGuid();

                if (userId == Guid.Empty) continue;
                if (subId == Guid.Empty) continue;

                yield return (userId, subId);
            }
        }

        public async Task<GenericSubscriptionRecord?> GetById(Guid userId, Guid subId)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Generic_Subscription
                    WHERE
                        UserID = @UserID
                        AND InternalSubscriptionID = @InternalSubscriptionID;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserID", userId.ToString()),
                    new MySqlParameter("InternalSubscriptionID", subId.ToString()),
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseSubscriptionRecord();

                    return record;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<GenericSubscriptionRecord?> GetByProcessorId(string processorSubId)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Generic_Subscription
                    WHERE
                        ProcessorSubscriptionID = @ProcessorSubscriptionID;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("ProcessorSubscriptionID", processorSubId),
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseSubscriptionRecord();

                    return record;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public Task Save(GenericSubscriptionRecord record)
        {
            return InsertOrUpdate(record);
        }

        private async Task InsertOrUpdate(GenericSubscriptionRecord record)
        {
            try
            {
                const string query = @"
                    INSERT INTO Payment_Generic_Subscription
                            (InternalSubscriptionID,  UserID,  ProcessorName,  ProcessorCustomerID,  ProcessorSubscriptionID,
                             Status,  AmountCents,  TaxCents,  TaxRateThousandPercents,  TotalCents,
                             CreatedOnUTC,  CreatedBy,  ModifiedOnUTC,  ModifiedBy,  CanceledOnUTC,  CanceledBy,
                             CardExpYear,  CardExpMonth,  CardLast4,  OldSubscriptionID)
                    VALUES (@InternalSubscriptionID, @UserID, @ProcessorName, @ProcessorCustomerID, @ProcessorSubscriptionID,
                            @Status, @AmountCents, @TaxCents, @TaxRateThousandPercents, @TotalCents,
                            @CreatedOnUTC, @CreatedBy, @ModifiedOnUTC, @ModifiedBy, @CanceledOnUTC, @CanceledBy,
                            @CardExpYear, @CardExpMonth, @CardLast4, @OldSubscriptionID)
                    ON DUPLICATE KEY UPDATE
                            UserID = @UserID,
                            ProcessorName = @ProcessorName,
                            ProcessorCustomerID = @ProcessorCustomerID,
                            ProcessorSubscriptionID = @ProcessorSubscriptionID,
                            Status = @Status,
                            AmountCents = @AmountCents,
                            TaxCents = @TaxCents,
                            TaxRateThousandPercents = @TaxRateThousandPercents,
                            TotalCents = @TotalCents,
                            ModifiedOnUTC = @ModifiedOnUTC,
                            ModifiedBy = @ModifiedBy,
                            CanceledOnUTC = @CanceledOnUTC,
                            CanceledBy = @CanceledBy,
                            CardExpYear = @CardExpYear,
                            CardExpMonth = @CardExpMonth,
                            CardLast4 = @CardLast4,
                            OldSubscriptionID = @OldSubscriptionID
                ";

                var parameters = new List<MySqlParameter>()
                {
                    new MySqlParameter("InternalSubscriptionID", record.InternalSubscriptionID),
                    new MySqlParameter("UserID", record.UserID),
                    new MySqlParameter("ProcessorName", record.ProcessorName),
                    new MySqlParameter("ProcessorCustomerID", record.ProcessorCustomerID),
                    new MySqlParameter("ProcessorSubscriptionID", record.ProcessorSubscriptionID),
                    new MySqlParameter("Status", record.Status),
                    new MySqlParameter("AmountCents", record.AmountCents),
                    new MySqlParameter("TaxCents", record.TaxCents),
                    new MySqlParameter("TaxRateThousandPercents", record.TaxRateThousandPercents),
                    new MySqlParameter("TotalCents", record.TotalCents),
                    new MySqlParameter("CreatedOnUTC", record.CreatedOnUTC.ToDateTime()),
                    new MySqlParameter("CreatedBy", record.CreatedBy),
                    new MySqlParameter("ModifiedOnUTC", record.ModifiedOnUTC?.ToDateTime()),
                    new MySqlParameter("ModifiedBy", record.ModifiedBy.Length == 36 ? record.ModifiedBy : null),
                    new MySqlParameter("CanceledOnUTC", record.CanceledOnUTC?.ToDateTime()),
                    new MySqlParameter("CanceledBy", record.CanceledBy.Length == 36 ? record.CanceledBy : null),
                    new MySqlParameter("CardExpYear", record.CardExpYear),
                    new MySqlParameter("CardExpMonth", record.CardExpMonth),
                    new MySqlParameter("CardLast4", record.CardLast4),
                    new MySqlParameter("OldSubscriptionID", record.OldSubscriptionID),
                };

                await sql.RunCmd(query, parameters.ToArray());
            }
            catch (Exception)
            {
            }
        }
    }
}
