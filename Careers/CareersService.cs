using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Careers.Data;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Fragments.Careers;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Careers
{
    [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
    public class CareersService : CareersInterface.CareersInterfaceBase
    {
        private readonly ILogger<CareersService> logger;
        private readonly ICareersDataProvider dataProvider;
        private readonly OfflineHelper offlineHelper;

        public CareersService(ILogger<CareersService> logger, ICareersDataProvider dataProvider, OfflineHelper offlineHelper)
        {
            this.logger = logger;
            this.dataProvider = dataProvider;
            this.offlineHelper = offlineHelper;
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<CreateCareerResponse> CreateCareer(CreateCareerRequest request, ServerCallContext context)
        {
            try
            {
                if (offlineHelper.IsOffline)
                    return new CreateCareerResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };
                var validator = new ProtoValidate.Validator();
                var validationResult = validator.Validate(request, false);

                if (validationResult.Violations.Count > 0)
                {
                    var validationError = GenericErrorExtensions.FromProtoValidateResult(
                        validationResult,
                        APIErrorReason.ErrorReasonValidationFailed,
                        "Validation failed"
                    );

                    return new CreateCareerResponse() { Error = validationError };
                }

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
                    CareerId = saved.CareerId,
                    Error = new Fragments.APIError
                    {
                        Reason = Fragments.APIErrorReason.ErrorReasonNoError
                    }
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
            if (offlineHelper.IsOffline)
                return new GetCareerResponse
                {
                    Error = GenericErrorExtensions.CreateOfflineError()
                };
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

                if (offlineHelper.IsOffline)
                    return new ListCareersResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };
                List<CareerListRecord> records = new();
                var res = new ListCareersResponse();

                await foreach (var record in dataProvider.GetAll())
                {
                    if (record.DeletedOnUTC == null)
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
        public override async Task<ListCareersResponse> AdminListCareers(AdminListCareersRequest request, ServerCallContext context)
        {
            try
            {
                if (offlineHelper.IsOffline)
                    return new ListCareersResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };
                List<CareerListRecord> records = new();
                var res = new ListCareersResponse();

                await foreach (var record in dataProvider.GetAll())
                {
                    if (record.DeletedOnUTC == null)
                        records.Add(record.ToCareerListRecord());

                    if (record.DeletedOnUTC != null && request.IncludeDeleted == true)
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
            catch (Exception e)
            {
                logger.LogError(e.Message);
                return new ListCareersResponse
                {
                    Error = new APIError
                    {
                        Reason = APIErrorReason.ErrorReasonUnknown,
                        Message = e.Message
                    }
                };
            }
            throw new NotImplementedException();
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<UpdateCareerResponse> UpdateCareer(UpdateCareerRequest request, ServerCallContext context)
        {
            try
            {
                if (offlineHelper.IsOffline)
                    return new UpdateCareerResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };

                var validator = new ProtoValidate.Validator();
                var validationResult = validator.Validate(request, false);
                if (validationResult.Violations.Count > 0)
                {
                    var validationError = GenericErrorExtensions.FromProtoValidateResult(
                        validationResult,
                        APIErrorReason.ErrorReasonValidationFailed,
                        "Validation failed"
                    );

                    return new UpdateCareerResponse { Error = validationError };
                }

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

                var career = request.Career;
                career.ModifiedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow);

                var updated = await dataProvider.Save(career);

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
                if (offlineHelper.IsOffline)
                    return new DeleteCareerResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };
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
