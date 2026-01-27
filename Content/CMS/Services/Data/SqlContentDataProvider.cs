using IT.WebServices.Content.CMS.Services.Helpers;
using IT.WebServices.Fragments.Content;
using IT.WebServices.Helpers;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace IT.WebServices.Content.CMS.Services.Data
{
    internal class SqlContentDataProvider : IContentDataProvider
    {
        private readonly MySQLHelper sql;
        private readonly SqlContentCategoryDataProvider categoryProvider;
        private readonly SqlContentChannelDataProvider channelProvider;

        public SqlContentDataProvider(MySQLHelper sql, SqlContentCategoryDataProvider categoryProvider, SqlContentChannelDataProvider channelProvider)
        {
            this.sql = sql;
            this.categoryProvider = categoryProvider;
            this.channelProvider = channelProvider;
        }

        public async Task<bool> Delete(Guid contentId)
        {
            try
            {
                const string query = @"
                    DELETE FROM
                        CMS_Content
                    WHERE
                        ContentID = @ContentID;

                    DELETE FROM
                        CMS_Category
                    WHERE
                        ContentID = @ContentID;

                    DELETE FROM
                        CMS_Channel
                    WHERE
                        ContentID = @ContentID;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("ContentID", contentId.ToString())
                };

                await sql.RunCmd(query, parameters);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> Exists(Guid contentId)
        {
            try
            {
                const string query = @"
                    SELECT
                        1
                    FROM
                        CMS_Content
                    WHERE
                        ContentID = @ContentID
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("ContentID", contentId.ToString())
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                    return true;

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async IAsyncEnumerable<ContentRecord> GetAll()
        {
            const string query = @"
                    SELECT
                        *
                    FROM
                        CMS_Content
                ";

            using var rdr = await sql.ReturnReader(query);

            while (await rdr.ReadAsync())
            {
                var record = rdr.ParseContentRecord();

                //await Task.WhenAll(
                //        categoryProvider.Load(record),
                //        channelProvider.Load(record)
                //    );

                yield return record;
            }
        }

        public async Task<ContentRecord> GetById(Guid contentId)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        CMS_Content
                    WHERE
                        ContentID = @ContentID;

                    SELECT
                        CategoryID
                    FROM
                        CMS_Category
                    WHERE
                        ContentID = @ContentID;

                    SELECT
                        ChannelID
                    FROM
                        CMS_Channel
                    WHERE
                        ContentID = @ContentID;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("ContentID", contentId.ToString())
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                return await LoadOneFromSqlReader(rdr);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<ContentRecord> GetByURL(string url)
        {
            try
            {
                const string query = @"
                    SELECT
                        @ContentID := ContentID
                    FROM
                        CMS_Content
                    WHERE
                        URL = @URL;

                    SELECT
                        *
                    FROM
                        CMS_Content
                    WHERE
                        ContentID = @ContentID;

                    SELECT
                        CategoryID
                    FROM
                        CMS_Category
                    WHERE
                        ContentID = @ContentID;

                    SELECT
                        ChannelID
                    FROM
                        CMS_Channel
                    WHERE
                        ContentID = @ContentID;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("URL", url)
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseContentRecord();

                    return await LoadOneFromSqlReader(rdr);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public Task Save(ContentRecord content)
        {
            return InsertOrUpdate(content);
        }

        private async Task InsertOrUpdate(ContentRecord content)
        {
            try
            {
                const string query = @"
                    INSERT INTO CMS_Content
                            (ContentID,  Type,  Title,  Description,  Author,  AuthorID,  URL,  FeaturedImageAssetID,  SubscriptionLevel,  HtmlBody,  AudioAssetID,
                             RumbleVideoId,  YoutubeVideoId,  IsLiveStream,  IsLive,  OldContentID,  CreatedOnUTC,  CreatedBy,  ModifiedOnUTC,  ModifiedBy,  PublishOnUTC,
                             PublishedBy,  AnnounceOnUTC,  AnnouncedBy,  PinnedOnUTC,  PinnedBy,  DeletedOnUTC,  DeletedBy)
                    VALUES (@ContentID, @Type, @Title, @Description, @Author, @AuthorID, @URL, @FeaturedImageAssetID, @SubscriptionLevel, @HtmlBody, @AudioAssetID,
                            @RumbleVideoId, @YoutubeVideoId, @IsLiveStream, @IsLive, @OldContentID, @CreatedOnUTC, @CreatedBy, @ModifiedOnUTC, @ModifiedBy, @PublishOnUTC,
                            @PublishedBy, @AnnounceOnUTC, @AnnouncedBy, @PinnedOnUTC, @PinnedBy, @DeletedOnUTC, @DeletedBy)
                    ON DUPLICATE KEY UPDATE
                            Type = @Type,
                            Title = @Title,
                            Description = @Description,
                            Author = @Author,
                            AuthorID = @AuthorID,
                            URL = @URL,
                            FeaturedImageAssetID = @FeaturedImageAssetID,
                            SubscriptionLevel = @SubscriptionLevel,
                            HtmlBody = @HtmlBody,
                            AudioAssetID = @AudioAssetID,
                            RumbleVideoId = @RumbleVideoId,
                            YoutubeVideoId = @YoutubeVideoId,
                            IsLiveStream = @IsLiveStream,
                            IsLive = @IsLive,
                            OldContentID = @OldContentID,
                            ModifiedOnUTC = @ModifiedOnUTC,
                            ModifiedBy = @ModifiedBy,
                            PublishOnUTC = @PublishOnUTC,
                            PublishedBy = @PublishedBy,
                            AnnounceOnUTC = @AnnounceOnUTC,
                            AnnouncedBy = @AnnouncedBy,
                            PinnedOnUTC = @PinnedOnUTC,
                            PinnedBy = @PinnedBy,
                            DeletedOnUTC = @DeletedOnUTC,
                            DeletedBy = @DeletedBy;
                ";

                var parameters = new List<MySqlParameter>()
                {
                    new MySqlParameter("ContentID", content.Public.ContentID),
                    new MySqlParameter("Type", content.Public.Data.ContentDataOneofCase.ToString()),
                    new MySqlParameter("Title", content.Public.Data.Title),
                    new MySqlParameter("Description", content.Public.Data.Description),
                    new MySqlParameter("Author", content.Public.Data.Author),
                    new MySqlParameter("AuthorID", string.Join(",", content.Public.Data.AuthorID)),
                    new MySqlParameter("URL", content.Public.Data.URL),
                    new MySqlParameter("FeaturedImageAssetID", content.Public.Data.FeaturedImageAssetID),
                    new MySqlParameter("SubscriptionLevel", content.Public.Data.SubscriptionLevel),
                    new MySqlParameter("OldContentID", content.Private.Data.OldContentID),
                    new MySqlParameter("CreatedOnUTC", content.Public.CreatedOnUTC.ToDateTime()),
                    new MySqlParameter("CreatedBy", content.Private.CreatedBy),
                    new MySqlParameter("ModifiedOnUTC", content.Public.ModifiedOnUTC?.ToDateTime()),
                    new MySqlParameter("ModifiedBy", content.Private.ModifiedBy.Length == 36 ? content.Private.ModifiedBy : null),
                    new MySqlParameter("PublishOnUTC", content.Public.PublishOnUTC?.ToDateTime()),
                    new MySqlParameter("PublishedBy", content.Private.PublishedBy.Length == 36 ? content.Private.PublishedBy : null),
                    new MySqlParameter("AnnounceOnUTC", content.Public.AnnounceOnUTC?.ToDateTime()),
                    new MySqlParameter("AnnouncedBy", content.Private.AnnouncedBy.Length == 36 ? content.Private.AnnouncedBy : null),
                    new MySqlParameter("PinnedOnUTC", content.Public.PinnedOnUTC?.ToDateTime()),
                    new MySqlParameter("PinnedBy", content.Private.PinnedBy.Length == 36 ? content.Private.PinnedBy : null),
                    new MySqlParameter("DeletedOnUTC", content.Public.DeletedOnUTC?.ToDateTime()),
                    new MySqlParameter("DeletedBy", content.Private.DeletedBy.Length == 36 ? content.Private.DeletedBy : null)
                };

                switch (content.Public.Data.ContentDataOneofCase)
                {
                    case ContentPublicData.ContentDataOneofOneofCase.Audio:
                        parameters.Add(new MySqlParameter("AudioAssetID", content.Public.Data.Audio.AudioAssetID));
                        parameters.Add(new MySqlParameter("HtmlBody", content.Public.Data.Audio.HtmlBody));
                        break;
                    case ContentPublicData.ContentDataOneofOneofCase.Picture:
                        parameters.Add(new MySqlParameter("HtmlBody", content.Public.Data.Picture.HtmlBody));
                        //parameters.Add(new MySqlParameter("ImageAssetIDs", content.Public.Data.Picture.ImageAssetIDs));
                        break;
                    case ContentPublicData.ContentDataOneofOneofCase.Video:
                        parameters.Add(new MySqlParameter("HtmlBody", content.Public.Data.Video.HtmlBody));
                        parameters.Add(new MySqlParameter("RumbleVideoId", content.Public.Data.Video.RumbleVideoId));
                        parameters.Add(new MySqlParameter("YoutubeVideoId", content.Public.Data.Video.YoutubeVideoId));
                        parameters.Add(new MySqlParameter("IsLiveStream", content.Public.Data.Video.IsLiveStream));
                        parameters.Add(new MySqlParameter("IsLive", content.Public.Data.Video.IsLive));
                        break;
                    case ContentPublicData.ContentDataOneofOneofCase.Written:
                        parameters.Add(new MySqlParameter("HtmlBody", content.Public.Data.Written.HtmlBody));
                        break;
                }

                if (!parameters.Any(p => p.ParameterName == "AudioAssetID"))
                    parameters.Add(new MySqlParameter("AudioAssetID", System.DBNull.Value));
                if (!parameters.Any(p => p.ParameterName == "RumbleVideoId"))
                    parameters.Add(new MySqlParameter("RumbleVideoId", System.DBNull.Value));
                if (!parameters.Any(p => p.ParameterName == "YoutubeVideoId"))
                    parameters.Add(new MySqlParameter("YoutubeVideoId", System.DBNull.Value));
                if (!parameters.Any(p => p.ParameterName == "IsLiveStream"))
                    parameters.Add(new MySqlParameter("IsLiveStream", System.DBNull.Value));

                if (!parameters.Any(p => p.ParameterName == "IsLive"))
                    parameters.Add(new MySqlParameter("IsLive", System.DBNull.Value));

                await sql.RunCmd(query, parameters.ToArray());

                await categoryProvider.Update(content);
                await channelProvider.Update(content);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private async Task<ContentRecord> LoadOneFromSqlReader(DbDataReader rdr)
        {
            if (!await rdr.ReadAsync())
                return null;

            var record = rdr.ParseContentRecord();

            if (await rdr.NextResultAsync())
            {
                while (await rdr.ReadAsync())
                {
                    var id = rdr.GetString(0);
                    if (!string.IsNullOrWhiteSpace(id))
                        record.Public.Data.CategoryIds.Add(id);
                }
            }

            if (await rdr.NextResultAsync())
            {
                while (await rdr.ReadAsync())
                {
                    var id = rdr.GetString(0);
                    if (!string.IsNullOrWhiteSpace(id))
                        record.Public.Data.ChannelIds.Add(id);
                }
            }

            return record;
        }
    }
}
