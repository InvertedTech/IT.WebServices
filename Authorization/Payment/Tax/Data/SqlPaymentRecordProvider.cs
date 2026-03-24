using IT.WebServices.Authorization.Payment.Tax.Helpers;
using IT.WebServices.Fragments.Authorization.Payment.Tax;
using IT.WebServices.Helpers;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace IT.WebServices.Authorization.Payment.Tax.Data
{
    internal class SqlSalesTaxRecordProvider : ISalesTaxRecordProvider
    {
        private readonly MySQLHelper sql;
        private readonly ILogger log;

        public SqlSalesTaxRecordProvider(MySQLHelper sql, ILogger<SqlSalesTaxRecordProvider> log)
        {
            this.sql = sql;
            this.log = log;
        }

        public async Task Delete(string countryCode, string postalCode)
        {
            try
            {
                const string query = @"
                    DELETE FROM
                        Payment_Tax_SalesTax
                    WHERE
                        CountryCode = @CountryCode
                        AND PostalCode = @PostalCode
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("CountryCode", countryCode),
                    new MySqlParameter("PostalCode", postalCode),
                };

                await sql.RunCmd(query, parameters);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in Delete");
            }
        }

        public async Task DeleteForCountry(string countryCode)
        {
            try
            {
                const string query = @"
                    DELETE FROM
                        Payment_Tax_SalesTax
                    WHERE
                        CountryCode = @CountryCode
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("CountryCode", countryCode),
                };

                await sql.RunCmd(query, parameters);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in DeleteForCountry");
            }
        }

        public async Task<bool> Exists(string countryCode, string postalCode)
        {
            var rec = await Get(countryCode, postalCode);
            return rec != null;
        }

        public async Task<SalesTaxByPostalCodeRecord?> Get(string countryCode, string postalCode)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Tax_SalesTax
                    WHERE
                        CountryCode = @CountryCode
                        AND PostalCode = @PostalCode
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("CountryCode", countryCode),
                    new MySqlParameter("PostalCode", postalCode),
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseSalesTaxRecord();
                    return record;
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in Get");
            }

            return null;
        }

        public async IAsyncEnumerable<SalesTaxByPostalCodeRecord> GetAll()
        {
            const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Tax_SalesTax
                ";

            using var rdr = await sql.ReturnReader(query);

            while (await rdr.ReadAsync())
            {
                var record = rdr.ParseSalesTaxRecord();
                yield return record;
            }
        }

        public async IAsyncEnumerable<SalesTaxByPostalCodeRecord> GetAllByCountryCode(string countryCode)
        {
            const string query = @"
                    SELECT
                        *
                    FROM
                        Payment_Tax_SalesTax
                    WHERE
                        CountryCode = @CountryCode
                    ORDER BY
                        PostalCode ASC
                ";

            var parameters = new MySqlParameter[]
            {
                    new MySqlParameter("CountryCode", countryCode),
            };

            using var rdr = await sql.ReturnReader(query, parameters);

            while (await rdr.ReadAsync())
            {
                var record = rdr.ParseSalesTaxRecord();
                yield return record;
            }
        }
        public Task Save(SalesTaxByPostalCodeRecord record)
        {
            return InsertOrUpdate(record);
        }

        private async Task InsertOrUpdate(SalesTaxByPostalCodeRecord record)
        {
            try
            {
                const string query = @"
                    INSERT INTO Payment_Tax_SalesTax
                            (CountryCode,  PostalCode,  TaxRateThousandPercents)
                    VALUES (@CountryCode, @PostalCode, @TaxRateThousandPercents)
                    ON DUPLICATE KEY UPDATE
                            PostalCode = @PostalCode
                ";

                var parameters = new List<MySqlParameter>()
                {
                    new MySqlParameter("CountryCode", record.CountryCode),
                    new MySqlParameter("PostalCode", record.PostalCode),
                    new MySqlParameter("TaxRateThousandPercents", record.TaxRateThousandPercents),
                };

                await sql.RunCmd(query, parameters.ToArray());
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in InsertOrUpdate");
            }
        }
    }
}
