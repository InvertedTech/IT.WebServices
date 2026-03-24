using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IT.WebServices.Authorization.Discord.Data;
using IT.WebServices.Clients.Authentication;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Fragments.Authorization.Discord;
using IT.WebServices.Helpers;
using Microsoft.AspNetCore.Authorization;

namespace IT.WebServices.Authorization.Discord.Services
{
    [Authorize]
    public class DiscordService : DiscordInterface.DiscordInterfaceBase
    {
        private readonly ILogger<DiscordService> _logger;
        private readonly IMemberDataProvider _members;
        private readonly IDiscordTicketDataProvider _tickets;
        private readonly IShunDataProvider _shuns;
        private readonly OfflineHelper _offlineHelper;
        private readonly UserClient _userClient;

        public DiscordService(
            ILogger<DiscordService> logger,
            IMemberDataProvider members,
            IDiscordTicketDataProvider tickets,
            IShunDataProvider shuns ,
            OfflineHelper offlineHelper,
            UserClient userClient)
        {
            _logger = logger;
            _members = members;
            _tickets = tickets;
            _shuns = shuns;
            _offlineHelper = offlineHelper;
            _userClient = userClient;
        }

        public override async Task<CreateMemberRecordResponse> CreateMemberRecord(
            CreateMemberRecordRequest request, ServerCallContext context)
        {
            if (_offlineHelper.IsOffline)
                return new CreateMemberRecordResponse
                {
                    Error = GenericErrorExtensions.CreateOfflineError()
                };

            Guid.TryParse(request.UserId, out var userGuid);
            if (userGuid == Guid.Empty)
            {
                return new CreateMemberRecordResponse
                {
                    Error = new APIError
                    {
                        Message = "Invalid Guid Passed To request",
                        Reason = APIErrorReason.ErrorReasonInvalidRequest,
                    }
                };
            }

            if (await _members.Exists(userGuid))
            {
                return new CreateMemberRecordResponse
                {
                    Error = new APIError
                    {
                        Message = "Your Discord Account Is Already Linked To Your User ID",
                        Reason = APIErrorReason.ErrorReasonAlreadyExists
                    }
                };
            }

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            // TODO: Check User Active Subscription
            // TODO: Validate Subscription Expiration
            // TODO: Calculate Subscription Tiers

            var newMember = new DiscordMemberRecord
            {
                UserId = request.UserId,
                Public = new DiscordMemberPublicRecord { 
                    DiscordUserId = request.DiscordUserId,
                    DiscordUserName = request.DiscordUserName,
                    CreatedOnUTC = now,
                },
                Private = new DiscordMemberPrivateRecord
                {
                    CreatedById = request.UserId,
                },
                Server = new DiscordMemberServerRecord
                {
                    CreatedOnUTC = now
                }
            };

            var ok = await _members.Create(newMember);
            if (!ok)
            {
                return new CreateMemberRecordResponse
                {
                    Error = new APIError
                    {
                        Message = "Database Failed To Create Record",
                        Reason = APIErrorReason.ErrorReasonProviderError,
                    }
                };
            }

            return new CreateMemberRecordResponse
            {
                Error = GenericErrorExtensions.CreateNoError(),
                Record = newMember
            };
        }

        public override async Task<ModifyMemberRecordResponse> ModifyMemberRecord(
            ModifyMemberRecordRequest request, ServerCallContext context)
        {
            try
            {
                if (_offlineHelper.IsOffline)
                    return new ModifyMemberRecordResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };

                Guid.TryParse(request.UserId, out var userGuid);
                if (userGuid == Guid.Empty)
                {
                    return new ModifyMemberRecordResponse
                    {
                        Error = new APIError
                        {
                            Message = "Invalid Guid Passed To request",
                            Reason = APIErrorReason.ErrorReasonInvalidRequest,
                        }
                    };
                }

                var found = await _members.GetByUserId(userGuid);
                if (found == null)
                    return new ModifyMemberRecordResponse
                    {
                        Error = new APIError
                        {
                            Reason = APIErrorReason.ErrorReasonNotFound,
                            Message = $"User ID {request.UserId} Not Found In Database"
                        }
                    };

                // TODO: Map Values
                await _members.Save(found);
                return new ModifyMemberRecordResponse
                {
                    Error = GenericErrorExtensions.CreateNoError()
                };
            } catch (Exception ex)
            {
                return new ModifyMemberRecordResponse
                {
                    Error = new APIError
                    {
                        Reason = APIErrorReason.ErrorReasonProviderError,
                        Message = ex.Message,
                    }
                };
            }
        }

        public override async Task<GetMemberResponse> GetMemberByUserId(
            GetMemberByUserIdRequest request, ServerCallContext context)
        {
            if (_offlineHelper.IsOffline)
                return new GetMemberResponse
                {
                    Error = GenericErrorExtensions.CreateOfflineError()
                };

            Guid.TryParse(request.UserId, out var userGuid);
            if (userGuid == Guid.Empty)
            {
                return new GetMemberResponse
                {
                    Error = new APIError
                    {
                        Message = "Invalid Guid Passed To request",
                        Reason = APIErrorReason.ErrorReasonInvalidRequest,
                    }
                };
            }

            var foundUser = await _members.GetByUserId(userGuid);
            if (foundUser == null)
            {
                return new GetMemberResponse
                {
                    Error = new APIError
                    {
                        Message = $"UserID {request.UserId} Not Found In Discord",
                        Reason = APIErrorReason.ErrorReasonNotFound
                    }
                };
            }

            return new GetMemberResponse
            {
                Error = GenericErrorExtensions.CreateNoError(),
                Record = foundUser
            };
        }

        public override async Task<GetMemberResponse> GetMemberByDiscordId(
            GetMemberByDiscordIdRequest request, ServerCallContext context)
        {
            if (_offlineHelper.IsOffline)
                return new GetMemberResponse
                {
                    Error = GenericErrorExtensions.CreateOfflineError()
                };

            var foundUser = await _members.GetByDiscordId(request.DiscordUserId);
            if (foundUser == null)
            {
                return new GetMemberResponse
                {
                    Error = new APIError
                    {
                        Message = $"DiscordID {request.DiscordUserId} Not Found In Discord",
                        Reason = APIErrorReason.ErrorReasonNotFound
                    }
                };
            }

            return new GetMemberResponse
            {
                Error = GenericErrorExtensions.CreateNoError(),
                Record = foundUser
            };
        }

        public override async Task<GetMembersResponse> GetMembers(
            GetMembersRequest request, ServerCallContext context)
        {
            if (_offlineHelper.IsOffline)
                return new GetMembersResponse
                {
                    Error = GenericErrorExtensions.CreateOfflineError()
                };

            var res = new GetMembersResponse();
            var foundRecords = _members.GetAll();

            await foreach (var record in foundRecords)
            {
                if (request.IncludeBanned && record.Public.BannedOnUTC != null)
                    res.Record.Append(record);

                if (request.PossibleUserIds != null && request.PossibleUserIds.Contains(record.UserId))
                    res.Record.Append(record);
            }

            if (request.PageSize > 0)
            {
                var page = res.Record.Skip((int) request.PageOffset)
                    .Take((int) request.PageSize)
                    .ToList();
                res.Record.Clear();
                res.Record.AddRange(page);
            }

            res.PageTotalItems = (uint)res.Record.Count;
            res.PageOffsetStart = request.PageOffset;
            res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Record.Count;
            return res;
        }

        public override async Task<BanMemberResponse> BanMember(
            BanMemberRequest request, ServerCallContext context)
        {
            try
            {
                if (_offlineHelper.IsOffline)
                    return new BanMemberResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };

                Guid.TryParse(request.UserId, out var userId);
                if (userId == Guid.Empty)
                    return new BanMemberResponse
                    {
                        Error = new APIError
                        {
                            Message = "Invalid/Empty Id Passed To BanMemberRequest",
                            Reason = APIErrorReason.ErrorReasonInvalidRequest
                        }
                    };

                var foundUser = await _members.GetByUserId(userId);
                if (foundUser == null)
                    return new BanMemberResponse
                    {
                        Error = new APIError
                        {
                            Reason = APIErrorReason.ErrorReasonNotFound,
                            Message = $"UserID {request.UserId} Not Found In Database"
                        }
                    };

                var now = Timestamp.FromDateTime(DateTime.UtcNow);
                foundUser.Public.BannedOnUTC = now;
                foundUser.Public.ModifiedOnUTC = now;
                foundUser.Public.BannedReason = request.BannedReason;
                foundUser.Private.BannedByDiscordId = request.BannedByDiscordId;
                foundUser.Private.ModifiedById = request.BannedByDiscordId;                 // TODO: Parse Token Of Calling User And Get Guid

                await _members.Save(foundUser);
                return new BanMemberResponse
                {
                    Error = GenericErrorExtensions.CreateNoError()
                };
            } catch (Exception ex)
            {
                return new BanMemberResponse
                {
                    Error = new APIError
                    {
                        Message = ex.Message ?? "Error Banning User",
                        Reason = APIErrorReason.ErrorReasonProviderError
                    }
                };
            }
        }

        public override async Task<ShunDiscordMemberResponse> ShunDiscordMember(
            ShunDiscordMemberRequest request, ServerCallContext context)
        {
            try
            {
                if (_offlineHelper.IsOffline)
                    return new ShunDiscordMemberResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };

                Guid.TryParse(request.UserId, out var userId);
                if (userId == Guid.Empty)
                {
                    return new ShunDiscordMemberResponse
                    {
                        Error = new APIError
                        {
                            Message = "Invalid Guid Passed To Request.UserId",
                            Reason = APIErrorReason.ErrorReasonInvalidRequest,
                        }
                    };
                }

                var record = new DiscordShunRecord
                {
                    ShunId = Guid.NewGuid().ToString(),
                    UserId = request.UserId,
                    ShunnedByDiscordId = request.ShunnedByDiscordId,
                    Reason = request.Reason,
                    Status = ShunStatus.ShunActive,
                    CreatedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow)
                };
                var ok = await _shuns.Create(record);

                if (!ok)
                {
                    return new ShunDiscordMemberResponse
                    {
                        Error = new APIError
                        {
                            Reason = APIErrorReason.ErrorReasonProviderError,
                            Message = "Database returned false when creating this record"
                        }
                    };
                }

                return new ShunDiscordMemberResponse
                {
                    Record = record,
                    Error = GenericErrorExtensions.CreateNoError()
                };
            } catch (Exception ex)
            {
                return new ShunDiscordMemberResponse
                {
                    Error = new APIError
                    {
                        Message = ex.Message,
                        Reason = APIErrorReason.ErrorReasonProviderError
                    }
                };
            }
        }

        public override async Task<UnShunDiscordMemberResponse> UnShunDiscordMember(
            UnShunDiscordMemberRequest request, ServerCallContext context)
        {
            try
            {
                if (_offlineHelper.IsOffline)
                    return new UnShunDiscordMemberResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };

                Guid.TryParse(request.UserId, out var userId);
                if (userId == Guid.Empty)
                {
                    return new UnShunDiscordMemberResponse
                    {
                        Error = new APIError
                        {
                            Message = "Invalid Guid Passed To Request.UserId",
                            Reason = APIErrorReason.ErrorReasonInvalidRequest,
                        }
                    };
                }

                Guid.TryParse(request.ShunId, out var shunId);
                if (shunId == Guid.Empty)
                {
                    return new UnShunDiscordMemberResponse
                    {
                        Error = new APIError
                        {
                            Message = "Invalid Guid Passed To Request.ShunId",
                            Reason = APIErrorReason.ErrorReasonInvalidRequest,
                        }
                    };
                }

                var foundShun = await _shuns.GetById(shunId);
                if (foundShun == null)
                {
                    return new UnShunDiscordMemberResponse
                    {
                        Error = new APIError
                        {
                            Reason = APIErrorReason.ErrorReasonNotFound,
                            Message = $"Shun With ID {request.ShunId} Not found"
                        }
                    };
                }

                // TODO: Parse Calling User Token and Persist To ShunRecord
                foundShun.Status = ShunStatus.ShunInactive;
                foundShun.UnShunnedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow);
                foundShun.UnShunnedByDiscordId = request.UnShunnedByDiscordId;

                await _shuns.Save(foundShun);

                return new UnShunDiscordMemberResponse
                {
                    Error = GenericErrorExtensions.CreateNoError(),
                };
            } catch (Exception ex)
            {
                return new UnShunDiscordMemberResponse
                {
                    Error = new APIError
                    {
                        Message = ex.Message,
                        Reason = APIErrorReason.ErrorReasonProviderError
                    }
                };
            }
        }

        public override async Task<GetShunsResponse> GetShuns(
            GetShunsRequest request, ServerCallContext context)
        {
            var res = new GetShunsResponse { };
            if (_offlineHelper.IsOffline)
            {
                res.Error = GenericErrorExtensions.CreateOfflineError();
                return res;
            }
            var foundShuns = _shuns.GetAll();
            await foreach (var shun in foundShuns)
            {
                if (!string.IsNullOrEmpty(request.UserId) && shun.UserId == request.UserId)
                    res.Records.Add(shun);
            }

            // TODO: Add Other Filters
            if (request.PageSize > 0)
            {
                var page = res.Records.Skip((int)request.PageOffset)
                    .Take((int)request.PageSize)
                    .ToList();
                res.Records.Clear();
                res.Records.AddRange(page);
            }

            res.PageTotalItems = (uint)res.Records.Count;
            res.PageOffsetStart = request.PageOffset;
            res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;
            return res;
        }

        public override async Task<CreateTicketResponse> CreateTicket(
            CreateTicketRequest request, ServerCallContext context)
        {
            try
            {
                if (_offlineHelper.IsOffline)
                    return new CreateTicketResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };

                // TODO: Fill Out
                var newTicket = new DiscordTicketRecord
                {

                };

                var ok = await _tickets.Create(newTicket);
                if (!ok)
                    return new CreateTicketResponse
                    {
                        Error = new APIError
                        {
                            Message = "Db Returned False For OK",
                            Reason = APIErrorReason.ErrorReasonProviderError,
                        }
                    };

                return new CreateTicketResponse
                {
                    Error = GenericErrorExtensions.CreateNoError(),
                    Record = newTicket
                };
            } catch (Exception ex)
            {
                return new CreateTicketResponse
                {
                    Error = new APIError
                    {
                        Message = ex.Message,
                        Reason = APIErrorReason.ErrorReasonProviderError,
                    }
                };
            }
        }

        public override async Task<AddTicketMessageResponse> AddTicketMessage(
            AddTicketMessageRequest request, ServerCallContext context)
        {
            try
            {
                if (_offlineHelper.IsOffline)
                    return new AddTicketMessageResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };

                Guid.TryParse(request.UserId, out var userId);
                if (userId == Guid.Empty)
                    return new AddTicketMessageResponse
                    {
                        Error = new APIError
                        {
                            Message = "Invalid Guid Passed To Request.ShunId",
                            Reason = APIErrorReason.ErrorReasonInvalidRequest,
                        }
                    };

                Guid.TryParse(request.TicketId, out var ticketId);
                if (ticketId == Guid.Empty)
                    return new AddTicketMessageResponse
                    {
                        Error = new APIError
                        {
                            Message = "Invalid Guid Passed To Request.ShunId",
                            Reason = APIErrorReason.ErrorReasonInvalidRequest,
                        }
                    };

                var found = await _tickets.GetById(ticketId);
                if (found == null)
                {
                    return new AddTicketMessageResponse
                    {
                        Error = new APIError
                        {
                            Reason = APIErrorReason.ErrorReasonNotFound,
                            Message = $"Ticket With ID {request.TicketId} Not found"
                        }
                    };
                }
                var newMessage = new DiscordTicketThreadMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    UserId = request.UserId,
                    TicketId = request.TicketId,
                    SentByDiscordId = request.SentByDiscordId,
                    Text = request.Text,
                    CreatedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow),
                };

                // TODO: Persist Message To DB
                found.Messages.Add(newMessage);

                await _tickets.Save(found);
                return new AddTicketMessageResponse
                {
                    Record = newMessage,
                    Error = GenericErrorExtensions.CreateNoError()
                };
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error While Running DiscordService.AddTicketMessage");
                return new AddTicketMessageResponse
                {
                    Error = new APIError
                    {
                        Reason = APIErrorReason.ErrorReasonProviderError,
                        Message = ex.Message,
                    }
                };
            }
        }

        public override async Task<CloseTicketResponse> CloseTicket(
            CloseTicketRequest request, ServerCallContext context)
        {
            try
            {
                if (_offlineHelper.IsOffline)
                    return new CloseTicketResponse
                    {
                        Error = GenericErrorExtensions.CreateOfflineError()
                    };

                Guid.TryParse(request.TicketId, out var ticketId);
                if (ticketId == Guid.Empty)
                    return new CloseTicketResponse
                    {
                        Error = new APIError
                        {
                            Message = "Invalid Guid Passed To Request.ShunId",
                            Reason = APIErrorReason.ErrorReasonInvalidRequest,
                        }
                    };

                var found = await _tickets.GetById(ticketId);
                if (found == null)
                {
                    return new CloseTicketResponse
                    {
                        Error = new APIError
                        {
                            Reason = APIErrorReason.ErrorReasonNotFound,
                            Message = $"Ticket With ID {request.TicketId} Not found"
                        }
                    };
                }

                found.Status = TicketStatus.TicketClosed;
                found.ClosedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow);
                found.ClosedByDiscordId = request.ClosedByDiscordId;

                await _tickets.Save(found);
                return new CloseTicketResponse
                {
                    Error = GenericErrorExtensions.CreateNoError(),
                    Record = found
                };
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error In DiscordService.CloseTicket");
                return new CloseTicketResponse
                {
                    Error = new APIError
                    {
                        Reason = APIErrorReason.ErrorReasonProviderError,
                        Message = ex.Message,
                    }
                };
            }
        }

        public override async Task<GetTicketsResponse> GetTickets(
            GetTicketsRequest request, ServerCallContext context)
        {
            var res = new GetTicketsResponse { };
            if (_offlineHelper.IsOffline)
            {
                res.Error = GenericErrorExtensions.CreateOfflineError();
                return res;
            }

            var foundTickets = _tickets.GetAll();
            await foreach(var tickets in foundTickets)
            {
                if (!string.IsNullOrEmpty(request.UserId) && tickets.UserId == request.UserId)
                    res.Records.Add(tickets);

                var ticketClosed = tickets.ClosedOnUTC >= Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);
                if (ticketClosed && request.IncludeClosed)
                    res.Records.Add(tickets);
            }

            if (request.PageSize > 0)
            {
                var page = res.Records.Skip((int)request.PageOffset)
                    .Take((int)request.PageSize)
                    .ToList();
                res.Records.Clear();
                res.Records.AddRange(page);
            }

            res.PageTotalItems = (uint)res.Records.Count;
            res.PageOffsetStart = request.PageOffset;
            res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;

            return res;
        }
    }
}
