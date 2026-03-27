using IT.WebServices.Fragments.Merch;
using System;
using System.Collections.Generic;
using System.Text;

namespace IT.WebServices.Merch.Jobs
{
    public interface IPullImagesFromAllProcessor
    {
        public Task Run(IBulkJob job);
    }
}
