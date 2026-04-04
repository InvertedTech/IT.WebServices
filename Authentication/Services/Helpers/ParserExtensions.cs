using Google.Protobuf;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Fragments.Generic;
using System;
using System.Data.Common;

namespace IT.WebServices.Authentication.Services.Helpers
{
    public static class ParserExtensions
    {
        public static UserRecord ParseUserRecord(this DbDataReader rdr)
        {
            var userRecord = new UserRecord()
            {
                Normal = new()
                {
                    Public = new()
                    {
                        UserID = rdr["UserID"] as string,
                        Data = new()
                        {
                            Bio = rdr["Bio"] as string ?? "",
                            DisplayName = rdr["DisplayName"] as string ?? "",
                            UserName = rdr["UserName"] as string ?? "",
                        },
                    },
                    Private = new()
                    {
                        CreatedBy = rdr["CreatedBy"] as string ?? "",
                        ModifiedBy = rdr["ModifiedBy"] as string ?? "",
                        DisabledBy = rdr["DisabledBy"] as string ?? "",
                        Data = new()
                        {
                            Email = rdr["Email"] as string ?? "",
                            FirstName = rdr["FirstName"] as string ?? "",
                            LastName = rdr["LastName"] as string ?? "",
                            PostalCode = rdr["PostalCode"] as string ?? "",
                            OldUserID = rdr["OldUserID"] as string ?? "",
                        },
                    },
                },
                Server = new()
                {
                    PasswordHash = ByteString.CopyFrom(rdr["PasswordHash"] as byte[]),
                    PasswordSalt = ByteString.CopyFrom(rdr["PasswordSalt"] as byte[]),
                    OldPassword = rdr["OldPassword"] as string ?? "",
                    OldPasswordAlgorithm = rdr["OldPasswordAlgorithm"] as string ?? "",
                    AuthProviders = new()
                    {
                        Microsoft = new()
                        {
                            UserId = rdr["MicrosoftAuthProviderUserId"] as string ?? "",
                        },
                    },
                },
            };

            userRecord.Normal.Private.Roles.AddRange((rdr["Roles"] as string)?.Split(','));

            DateTime d;
            if (!(rdr["CreatedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["CreatedOnUTC"], DateTimeKind.Utc);
                userRecord.Normal.Public.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["ModifiedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["ModifiedOnUTC"], DateTimeKind.Utc);
                userRecord.Normal.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["DisabledOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["DisabledOnUTC"], DateTimeKind.Utc);
                userRecord.Normal.Public.DisabledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            return userRecord;
        }

        public static (Guid id, TOTPDevice record) ParseTOTPDevice(this DbDataReader rdr)
        {
            var id = (rdr["UserID"] as string)?.ToGuid() ?? Guid.Empty;

            var record = new TOTPDevice()
            {
                TotpID = rdr["TotpID"] as string,
                DeviceName = rdr["DeviceName"] as string,
                Key = ByteString.CopyFrom(rdr["Key"] as byte[]),
            };

            DateTime d;
            if (!(rdr["CreatedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["CreatedOnUTC"], DateTimeKind.Utc);
                record.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["VerifiedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["VerifiedOnUTC"], DateTimeKind.Utc);
                record.VerifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["DisabledOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["DisabledOnUTC"], DateTimeKind.Utc);
                record.DisabledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }


            return (id, record);
        }
    }
}
