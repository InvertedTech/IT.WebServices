using IT.WebServices.Fragments.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pb = global::Google.Protobuf;

namespace IT.WebServices.Fragments.Careers
{
    public sealed partial class CareerRecord : pb::IMessage<CareerRecord>
    {
        public CareerListRecord ToCareerListRecord()
        {
            var rec = new CareerListRecord()
            {
                CareerId = this.CareerId,
                Title = this.Title,
                Company = this.Company,
                Location = this.Location,
                About = this.About,
                CreatedOnUTC = this.CreatedOnUTC,
                ModifiedOnUTC = this.ModifiedOnUTC,
                DeletedOnUTC = this.DeletedOnUTC,
            };

            rec.Responsibilities.AddRange(this.Responsibilities);

            return rec;
        }
    }
}
