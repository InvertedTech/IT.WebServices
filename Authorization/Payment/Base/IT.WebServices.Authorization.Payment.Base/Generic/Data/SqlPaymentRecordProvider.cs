using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using MySql.Data.MySqlClient;
using System;

namespace IT.WebServices.Authorization.Payment.Generic.Data
{
    internal class SqlPaymentRecordProvider : IGenericPaymentRecordProvider
    {
        public readonly MySQLHelper sql;

        public SqlPaymentRecordProvider(MySQLHelper sql)
        {
            this.sql = sql;
        }

        public async Task Delete(Guid userId, Guid subId, Guid paymentId)
        {
            try
            {
                const string query = @"
                    DELETE FROM
                        Payment_Generic_Payment
                    WHERE
                        UserID = @UserID
                        AND InternalSubscriptionID = @InternalSubscriptionID
                        AND InternalPaymentID = @InternalPaymentID;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserID", userId.ToString()),
                    new MySqlParameter("InternalSubscriptionID", subId.ToString()),
                    new MySqlParameter("InternalPaymentID", paymentId.ToString()),
                };

                await sql.RunCmd(query, parameters);
            }
            catch (Exception)
            {
            }
        }

        public async Task DeleteAll(Guid userId, Guid subId)
        {
            try
            {
                const string query = @"
                    DELETE FROM
                        Payment_Generic_Payment
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

        public async Task<bool> Exists(Guid userId, Guid subId, Guid paymentId)
        {
            var rec = await GetById(userId, subId, paymentId);
            return rec != null;
        }

        public async IAsyncEnumerable<GenericPaymentRecord> GetAll()
        {
            const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Generic_Payment
                ";

            using var rdr = await sql.ReturnReader(query);

            while (await rdr.ReadAsync())
            {
                var record = rdr.ParsePaymentRecord();

                if (record != null)
                    yield return record;
            }
        }

        public async IAsyncEnumerable<GenericPaymentRecord> GetAllBySubscriptionId(Guid userId, Guid subId)
        {
            const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Generic_Payment
                    WHERE
                        UserID = @UserID
                        AND InternalSubscriptionID = @InternalSubscriptionID
                    ORDER BY
                        PaidOnUTC DESC
                ";

            var parameters = new MySqlParameter[]
            {
                    new MySqlParameter("UserID", userId.ToString()),
                    new MySqlParameter("InternalSubscriptionID", subId.ToString()),
            };

            using var rdr = await sql.ReturnReader(query, parameters);

            while (await rdr.ReadAsync())
            {
                var record = rdr.ParsePaymentRecord();

                if (record != null)
                    yield return record;
            }
        }

        public async IAsyncEnumerable<GenericPaymentRecord> GetAllByUserId(Guid userId)
        {
            const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Generic_Payment
                    WHERE
                        UserID = @UserID
                    ORDER BY
                        PaidOnUTC DESC
                ";

            var parameters = new MySqlParameter[]
            {
                    new MySqlParameter("UserID", userId.ToString())
            };

            using var rdr = await sql.ReturnReader(query, parameters);

            while (await rdr.ReadAsync())
            {
                var record = rdr.ParsePaymentRecord();

                if (record != null)
                    yield return record;
            }
        }

        public async IAsyncEnumerable<(Guid userId, Guid subId, Guid paymentId)> GetAllSubscriptionIds()
        {
            const string query = @"
                    SELECT
                        UserID,
                        InternalSubscriptionID,
                        InternalPaymentID
                    FROM
                        Payment_Generic_Payment
                ";

            using var rdr = await sql.ReturnReader(query);

            while (await rdr.ReadAsync())
            {
                var userId = (rdr["UserID"] as string ?? "").ToGuid();
                var subId = (rdr["InternalSubscriptionID"] as string ?? "").ToGuid();
                var paymentId = (rdr["InternalPaymentID"] as string ?? "").ToGuid();

                if (userId == Guid.Empty) continue;
                if (subId == Guid.Empty) continue;
                if (paymentId == Guid.Empty) continue;

                yield return (userId, subId, paymentId);
            }
        }

        public async Task<GenericPaymentRecord?> GetById(Guid userId, Guid subId, Guid paymentId)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Generic_Payment
                    WHERE
                        UserID = @UserID
                        AND InternalSubscriptionID = @InternalSubscriptionID
                        AND InternalPaymentID = @InternalPaymentID;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserID", userId.ToString()),
                    new MySqlParameter("InternalSubscriptionID", subId.ToString()),
                    new MySqlParameter("InternalPaymentID", paymentId.ToString()),
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParsePaymentRecord();

                    return record;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<GenericPaymentRecord?> GetByProcessorId(string processorPaymentId)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Generic_Payment
                    WHERE
                        ProcessorPaymentID = @ProcessorPaymentID
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("ProcessorPaymentID", processorPaymentId),
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParsePaymentRecord();

                    return record;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public Task Save(GenericPaymentRecord record)
        {
            return InsertOrUpdate(record);
        }

        private async Task InsertOrUpdate(GenericPaymentRecord record)
        {
            try
            {
                const string query = @"
                    INSERT INTO Payment_Generic_Payment
                            (InternalPaymentID,  InternalSubscriptionID,  UserID,  ProcessorPaymentID,  Status,
                             AmountCents,  TaxCents,  TaxRateThousandPercents,  TotalCents,
                             CreatedOnUTC,  CreatedBy,  ModifiedOnUTC,  ModifiedBy,  PaidOnUTC,  PaidThruUTC,
                             OldPaymentID)
                    VALUES (@InternalPaymentID, @InternalSubscriptionID, @UserID, @ProcessorPaymentID, @Status,
                            @AmountCents, @TaxCents, @TaxRateThousandPercents, @TotalCents,
                            @CreatedOnUTC, @CreatedBy, @ModifiedOnUTC, @ModifiedBy, @PaidOnUTC, @PaidThruUTC,
                            @OldPaymentID)
                    ON DUPLICATE KEY UPDATE
                            InternalSubscriptionID = @InternalSubscriptionID,
                            UserID = @UserID,
                            ProcessorPaymentID = @ProcessorPaymentID,
                            Status = @Status,
                            AmountCents = @AmountCents,
                            TaxCents = @TaxCents,
                            TaxRateThousandPercents = @TaxRateThousandPercents,
                            TotalCents = @TotalCents,
                            ModifiedOnUTC = @ModifiedOnUTC,
                            ModifiedBy = @ModifiedBy,
                            PaidOnUTC = @PaidOnUTC,
                            PaidThruUTC = @PaidThruUTC,
                            OldPaymentID = @OldPaymentID
                ";

                var parameters = new List<MySqlParameter>()
                {
                    new MySqlParameter("InternalPaymentID", record.InternalPaymentID),
                    new MySqlParameter("InternalSubscriptionID", record.InternalSubscriptionID),
                    new MySqlParameter("UserID", record.UserID),
                    new MySqlParameter("ProcessorPaymentID", record.ProcessorPaymentID),
                    new MySqlParameter("Status", record.Status),
                    new MySqlParameter("AmountCents", record.AmountCents),
                    new MySqlParameter("TaxCents", record.TaxCents),
                    new MySqlParameter("TaxRateThousandPercents", record.TaxRateThousandPercents),
                    new MySqlParameter("TotalCents", record.TotalCents),
                    new MySqlParameter("CreatedOnUTC", record.CreatedOnUTC.ToDateTime()),
                    new MySqlParameter("CreatedBy", record.CreatedBy),
                    new MySqlParameter("ModifiedOnUTC", record.ModifiedOnUTC?.ToDateTime()),
                    new MySqlParameter("ModifiedBy", record.ModifiedBy.Length == 36 ? record.ModifiedBy : null),
                    new MySqlParameter("PaidOnUTC", record.PaidOnUTC?.ToDateTime()),
                    new MySqlParameter("PaidThruUTC", record.PaidThruUTC?.ToDateTime()),
                    new MySqlParameter("OldPaymentID", record.OldPaymentID),
                };

                await sql.RunCmd(query, parameters.ToArray());
            }
            catch (Exception)
            {
            }
        }
    }
}
