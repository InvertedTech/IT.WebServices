using IT.WebServices.Fragments.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pb = global::Google.Protobuf;

namespace IT.WebServices.Fragments.Content
{
    public sealed partial class ContentListRecord : pb::IMessage<ContentListRecord>
    {
        public Guid ContentIDGuid
        {
            get => ContentID.ToGuid();
            set => ContentID = value.ToString();
        }
    }

    public sealed partial class ContentPublicData : pb::IMessage<ContentPublicData>
    {
        public ContentType GetContentType()
        {
            switch (ContentDataOneofCase)
            {
                case ContentDataOneofOneofCase.Audio:
                    return ContentType.ContentAudio;
                case ContentDataOneofOneofCase.Picture:
                    return ContentType.ContentPicture;
                case ContentDataOneofOneofCase.Written:
                    return ContentType.ContentWritten;
                case ContentDataOneofOneofCase.Video:
                    return ContentType.ContentVideo;
                default:
                    return ContentType.ContentNone;
            }
        }
    }

    public sealed partial class ContentPublicRecord : pb::IMessage<ContentPublicRecord>
    {
        public Guid ContentIDGuid
        {
            get => ContentID.ToGuid();
            set => ContentID = value.ToString();
        }

        public ContentListRecord ToContentListRecord()
        {
            var rec = new ContentListRecord()
            {
                ContentID = ContentID,
                CreatedOnUTC = CreatedOnUTC,
                PublishOnUTC = PublishOnUTC,
                PinnedOnUTC = PinnedOnUTC,
                Title = Data.Title,
                Description = Data.Description,
                SubscriptionLevel = Data.SubscriptionLevel,
                URL = Data.URL,
                Author = Data.Author,
                AuthorID = Data.AuthorID,
                FeaturedImageAssetID = Data.FeaturedImageAssetID,
                ContentType = Data.GetContentType(),
            };

            rec.CategoryIds.AddRange(Data.CategoryIds);
            rec.ChannelIds.AddRange(Data.ChannelIds);

            if (rec.ContentType == ContentType.ContentVideo)
            {
                rec.IsLiveStream = Data.Video.IsLiveStream;
                rec.IsLive = Data.Video.IsLive;
            }

            return rec;
        }
    }
}