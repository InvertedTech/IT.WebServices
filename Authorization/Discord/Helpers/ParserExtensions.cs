using IT.WebServices.Fragments.Authorization.Discord;
using System;
using System.Data.Common;

namespace IT.WebServices.Authorization.Discord.Helpers
{
    public static class ParserExtensions
    {
        public static DiscordMemberRecord ParseDiscordMemberRecord(this DbDataReader rdr)
        {
            var record = new DiscordMemberRecord()
            {
                UserId = rdr["UserId"] as string ?? "",
                Public = new()
                {
                    DiscordUserId = rdr["DiscordUserId"] as string ?? "",
                    DiscordUserName = rdr["DiscordUserName"] as string ?? "",
                    BannedReason = rdr["BannedReason"] as string ?? "",
                },
                Private = new()
                {
                    CreatedById = rdr["CreatedById"] as string ?? "",
                    ModifiedById = rdr["ModifiedById"] as string ?? "",
                    BannedByDiscordId = rdr["BannedByDiscordId"] as string ?? "",
                    InternalSubscriptionId = rdr["InternalSubscriptionId"] as string ?? "",
                },
                Server = new()
                {
                    AccessToken = rdr["AccessToken"] as string ?? "",
                    RefreshToken = rdr["RefreshToken"] as string ?? "",
                },
            };

            var tiers = rdr["Tiers"] as string;
            if (!string.IsNullOrEmpty(tiers))
                record.Public.Tiers.AddRange(tiers.Split(','));

            DateTime d;
            if (!(rdr["CreatedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["CreatedOnUTC"], DateTimeKind.Utc);
                record.Public.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["ModifiedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["ModifiedOnUTC"], DateTimeKind.Utc);
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["BannedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["BannedOnUTC"], DateTimeKind.Utc);
                record.Public.BannedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["AccessTokenExpiresOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["AccessTokenExpiresOnUTC"], DateTimeKind.Utc);
                record.Server.AccessTokenExpiresOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["TokenCreatedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["TokenCreatedOnUTC"], DateTimeKind.Utc);
                record.Server.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["TokenModifiedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["TokenModifiedOnUTC"], DateTimeKind.Utc);
                record.Server.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            return record;
        }

        public static DiscordTicketRecord ParseDiscordTicketRecord(this DbDataReader rdr)
        {
            var record = new DiscordTicketRecord()
            {
                TicketId = rdr["TicketId"] as string ?? "",
                UserId = rdr["UserId"] as string ?? "",
                ThreadId = rdr["ThreadId"] as string ?? "",
                Status = Enum.TryParse<TicketStatus>(rdr["Status"] as string, out var status) ? status : TicketStatus.TicketOpen,
                Subject = rdr["Subject"] as string ?? "",
                Text = rdr["Text"] as string ?? "",
                ClosedByDiscordId = rdr["ClosedByDiscordId"] as string ?? "",
            };

            DateTime d;
            if (!(rdr["CreatedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["CreatedOnUTC"], DateTimeKind.Utc);
                record.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["ClosedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["ClosedOnUTC"], DateTimeKind.Utc);
                record.ClosedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            return record;
        }

        public static DiscordShunRecord ParseDiscordShunRecord(this DbDataReader rdr)
        {
            var record = new DiscordShunRecord()
            {
                ShunId = rdr["ShunId"] as string ?? "",
                UserId = rdr["UserId"] as string ?? "",
                ShunnedByDiscordId = rdr["ShunnedByDiscordId"] as string ?? "",
                Reason = rdr["Reason"] as string ?? "",
                Status = Enum.TryParse<ShunStatus>(rdr["Status"] as string, out var status) ? status : ShunStatus.ShunActive,
                UnShunnedByDiscordId = rdr["UnShunnedByDiscordId"] as string ?? "",
            };

            DateTime d;
            if (!(rdr["CreatedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["CreatedOnUTC"], DateTimeKind.Utc);
                record.CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            if (!(rdr["UnShunnedOnUTC"] is DBNull))
            {
                d = DateTime.SpecifyKind((DateTime)rdr["UnShunnedOnUTC"], DateTimeKind.Utc);
                record.UnShunnedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(d);
            }

            return record;
        }
    }
}
