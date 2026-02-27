using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Careers.Data;
using IT.WebServices.Fragments.Careers;
using IT.WebServices.Fragments.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Careers
{
    [Authorize]
    public class CareersService : CareersInterface.CareersInterfaceBase
    {
        private readonly ILogger<CareersService> logger;
        private readonly ICareersDataProvider dataProvider;
    
        public CareersService(ILogger<CareersService> logger, ICareersDataProvider dataProvider)
        {
            this.logger = logger;
            this.dataProvider = dataProvider;
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<CreateCareerResponse> CreateCareer(CreateCareerRequest request, ServerCallContext context)
        {
            try
            {
                var newCareer = new CareerRecord
                {
                    CareerId = Guid.NewGuid().ToString(),
                    Title = request.Title,
                    Company = request.Company,
                    Location = request.Location,
                    ReportsTo = request.ReportsTo,
                    Contact = request.Contact,
                    About = request.About,
                    RoleOverview = request.RoleOverview,
                    CreatedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow)
                };

                newCareer.Responsibilities.AddRange(request.Responsibilities);
                newCareer.Qualifications.AddRange(request.Qualifications);
                newCareer.WeeklyDeliverables.AddRange(request.WeeklyDeliverables);

                var saved = await dataProvider.Save(newCareer);
                return new CreateCareerResponse
                {
                    CareerId = saved.CareerId
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                return new CreateCareerResponse
                {
                    Error = new Fragments.APIError
                    {
                        Message = "An Error Has Ocurred",
                        Reason = Fragments.APIErrorReason.ErrorReasonUnknown
                    }
                };
            }
        }

        [AllowAnonymous]
        public override async Task<GetCareerResponse> GetCareer(GetCareerRequest request, ServerCallContext context)
        {
            var found = await dataProvider.Get(request.CareerId.ToGuid());
            return new GetCareerResponse
            {
                Career = found
            };
        }

        [AllowAnonymous]
        public override async Task<ListCareersResponse> ListCareers(ListCareersRequest request, ServerCallContext context)
        {
            try
            {
                List<CareerListRecord> records = new();
                var res = new ListCareersResponse();

                await foreach (var record in dataProvider.GetAll())
                {
                    records.Add(record.ToCareerListRecord());
                }
                res.Careers.AddRange(records.OrderByDescending(r => r.CreatedOnUTC));
                res.PageTotalItems = (uint)res.Careers.Count();

                if (request.PageSize > 0)
                {
                    res.PageOffsetStart = request.PageOffset;
                    var page = res.Careers.Skip((int)request.PageOffset).Take((int)request.PageSize).ToList();
                    res.Careers.Clear();
                    res.Careers.AddRange(page);
                }

                res.PageOffsetEnd = res.PageOffsetStart - (uint)res.Careers.Count;
                return res;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                return new ListCareersResponse { };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<UpdateCareerResponse> UpdateCareer(UpdateCareerRequest request, ServerCallContext context)
        {
            // TODO: Get Career; If Not Found then return error else set career.modifiedOnUtc to current date time and then save
            try
            {
                var reqGuid = request.CareerId.ToGuid();
                var exists = await dataProvider.Exists(reqGuid);

                if (!exists)
                {
                    return new UpdateCareerResponse
                    {
                        Error = new Fragments.APIError
                        {
                            Message = "Career Not Found",
                            Reason = Fragments.APIErrorReason.ErrorReasonNotFound
                        }
                    };
                }

                var updated = await dataProvider.Save(new CareerRecord
                {
                    CareerId = request.Career.CareerId,
                    Title = request.Career.Title,
                    Company = request.Career.Company,
                    Location = request.Career.Location,
                    ReportsTo = request.Career.ReportsTo,
                    Contact = request.Career.Contact,
                    About = request.Career.About,
                    RoleOverview = request.Career.RoleOverview,
                    ModifiedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow)
                });

                if (updated == null)
                {
                    return new UpdateCareerResponse
                    {
                        Error = new Fragments.APIError
                        {
                            Reason = Fragments.APIErrorReason.ErrorReasonProviderError,
                            Message = "Failed To Update Career"
                        }
                    };
                }
                return new UpdateCareerResponse
                {
                    Record = updated,
                    Error = new Fragments.APIError
                    {
                        Reason = Fragments.APIErrorReason.ErrorReasonNoError
                    }
                };
            } catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                return new UpdateCareerResponse
                {
                    Error = new Fragments.APIError
                    {
                        Message = "An Error Ocurred",
                        Reason = Fragments.APIErrorReason.ErrorReasonUnknown
                    }
                };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<DeleteCareerResponse> DeleteCareer(DeleteCareerRequest request, ServerCallContext context)
        {
            try
            {
                var reqGuid = request.CareerId.ToGuid();
                var exists = await dataProvider.Exists(reqGuid);
                if (!exists)
                    return new DeleteCareerResponse
                    {
                        Error =
                        {
                            Message = "Career Not Found",
                            Reason = Fragments.APIErrorReason.ErrorReasonNotFound
                        }
                    };

                var record = await dataProvider.Get(reqGuid);
                record.DeletedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow);

                await dataProvider.Save(record);
                return new DeleteCareerResponse
                {
                    Error = new Fragments.APIError
                    {
                        Reason = Fragments.APIErrorReason.ErrorReasonNoError
                    }
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                return new DeleteCareerResponse
                {
                    Error =
                    {
                        Message = "An Error Has Ocurred",
                        Reason = Fragments.APIErrorReason.ErrorReasonUnknown
                    }
                };
            }
        }
    }
}
