using Google.Authenticator;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using IT.WebServices.Authentication.Services.Data;
using IT.WebServices.Authentication.Services.Helpers;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Fragments.Authorization;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Settings;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IT.WebServices.Helpers;
using SkiaSharp;
using IT.WebServices.Fragments;

namespace IT.WebServices.Authentication.Services
{
    [Authorize]
    public class UserService : UserInterface.UserInterfaceBase
    {
        private readonly OfflineHelper offlineHelper;
        private readonly ILogger<UserService> logger;
        private readonly IProfilePicDataProvider picProvider;
        private readonly IUserDataProvider dataProvider;
        private readonly ClaimsClient claimsClient;
        private readonly ISettingsService settingsService;
        private readonly TokenHelper tokenHelper;
        private readonly UserServiceInternal userServiceInternal;
        private static readonly HashAlgorithm hasher = SHA256.Create();
        private static readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();

        public UserService(OfflineHelper offlineHelper, ILogger<UserService> logger, IProfilePicDataProvider picProvider, IUserDataProvider dataProvider, ClaimsClient claimsClient, ISettingsService settingsService, TokenHelper tokenHelper, UserServiceInternal userServiceInternal)
        {
            this.offlineHelper = offlineHelper;
            this.logger = logger;
            this.picProvider = picProvider;
            this.dataProvider = dataProvider;
            this.claimsClient = claimsClient;
            this.settingsService = settingsService;
            this.tokenHelper = tokenHelper;
            this.userServiceInternal = userServiceInternal;

            //if (Program.IsDevelopment)
            //{
            EnsureDevOwnerLogin().Wait();
            //}
        }

        // TODO: Use ProtoValidate
        [AllowAnonymous]
        public override async Task<AuthenticateUserResponse> AuthenticateUser(
            AuthenticateUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new AuthenticateUserResponse {
                    Ok = false,
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonServiceUnavailable,
                        "Server Unavailable, Try Again Later"
                    )
                };
            
            var validationIssues = new List<ValidationIssue>();

            if (string.IsNullOrWhiteSpace(request.UserName))
            {
                validationIssues.Add(new ValidationIssue
                {
                    Field = "UserName",
                    Message = "UserName is Required",
                    Code = "Required"
                });
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                validationIssues.Add(new ValidationIssue
                {
                    Field = "Password",
                    Message = "Password Is Required",
                    Code = "Required"
                });
            }

            if (validationIssues.Count > 0)
            {
                var error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Invlalid Request Body");
                error.Validation.AddRange(validationIssues);
                return new AuthenticateUserResponse
                {
                    Ok = false,
                    Error = error,
                };
            }

            var user = await dataProvider.GetByLogin(request.UserName);
            if (user == null)
            {
                user = await dataProvider.GetByEmail(request.UserName);
                if (user == null)
                    return new AuthenticateUserResponse
                    {
                        Ok = false,
                        Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "User Not Found")
                    };
            }

            bool isCorrect = await IsPasswordCorrect(request.Password, user);

            if (!isCorrect)
                return new AuthenticateUserResponse
                {
                    Ok = false,
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonInvalidContent,
                        "Check Credentials and try Again"
                    )
                };

            if (!ValidateTotp(user.Server?.TOTPDevices, request.MFACode))
                return new AuthenticateUserResponse
                {
                    Ok = false,
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonInvalidCode,
                        "MFACode Invalid"
                    )
                };

            var otherClaims = await claimsClient.GetOtherClaims(user.UserIDGuid);

            return new AuthenticateUserResponse()
            {
                Ok = true,
                BearerToken = tokenHelper.GenerateToken(user.Normal, otherClaims),
                UserRecord = user.Normal,
            };
        }

        [Authorize(Roles = ONUser.ROLE_IS_MEMBER_MANAGER_OR_HIGHER)]
        public override async Task<ChangeOtherPasswordResponse> ChangeOtherPassword(
            ChangeOtherPasswordRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new ChangeOtherPasswordResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonServiceUnavailable,
                        "Service is offline"
                    )
                };

            try
            {
                // TODO: Reimplement This
                //if (!await AmIReallyAdmin(context))
                //    return new ChangeOtherPasswordResponse
                //    {
                //        Error = ErrorExtensions.CreateError(
                //            APIErrorReason.ChangeOtherPasswordErrorUnknown,
                //            "Admin access required"
                //        )
                //    };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new ChangeOtherPasswordResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonNotFound,
                            "User not found"
                        )
                    };

                byte[] salt = RandomNumberGenerator.GetBytes(16);
                record.Server.PasswordSalt = ByteString.CopyFrom(salt);
                record.Server.PasswordHash = ByteString.CopyFrom(
                    ComputeSaltedHash(request.NewPassword, salt)
                );

                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new ChangeOtherPasswordResponse
                {
                    Error = GenericErrorExtensions.CreateNoError() // Success case - no error
                };
            }
            catch
            {
                return new ChangeOtherPasswordResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonUnknown,
                        "An unexpected error occurred"
                    )
                };
            }
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ChangeOtherProfileImageResponse> ChangeOtherProfileImage(
            ChangeOtherProfileImageRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new ChangeOtherProfileImageResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonServiceUnavailable,
                        "Service is offline"
                    )
                };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new ChangeOtherProfileImageResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonUnauthorized,
                            "Insufficient Permissions"
                        )
                    };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new ChangeOtherProfileImageResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonNotFound,
                            "User not found"
                        )
                    };

                if (request?.ProfileImage == null || request.ProfileImage.IsEmpty)
                    return new ChangeOtherProfileImageResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonInvalidContent,
                            "Profile image data is required"
                        )
                    };

                using var ms = new MemoryStream();
                ms.Write(request.ProfileImage.ToArray());
                ms.Position = 0;
                using var image = SKBitmap.Decode(ms);

                if (image == null)
                    return new ChangeOtherProfileImageResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonInvalidContent,
                            "Invalid image format"
                        )
                    };

                var newInfo = image.Info;
                newInfo.Width = 200;
                newInfo.Height = 200;
                using var newImage = image.Resize(newInfo, SKFilterQuality.Medium);

                using MemoryStream memStream = new MemoryStream();
                using SKManagedWStream wstream = new SKManagedWStream(memStream);

                newImage.Encode(wstream, SKEncodedImageFormat.Png, 50);

                await picProvider.Save(request.UserID.ToGuid(), memStream.ToArray());

                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new ChangeOtherProfileImageResponse
                {
                    Error = GenericErrorExtensions.CreateNoError() // Success case - no error
                };
            }
            catch
            {
                return new ChangeOtherProfileImageResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonUnknown,
                        "An unexpected error occurred while processing the image"
                    )
                };
            }
        }

        public override async Task<ChangeOwnPasswordResponse> ChangeOwnPassword(
            ChangeOwnPasswordRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new ChangeOwnPasswordResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonServiceUnavailable,
                        "Service is offline"
                    )
                };

            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new ChangeOwnPasswordResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonUnauthenticated,
                            "User authentication required"
                        )
                    };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new ChangeOwnPasswordResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonNotFound,
                            "User record not found"
                        )
                    };

                var hash = ComputeSaltedHash(request.OldPassword, record.Server.PasswordSalt.Span);
                if (!CryptographicOperations.FixedTimeEquals(record.Server.PasswordHash.Span, hash))
                    return new ChangeOwnPasswordResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonUnauthenticated,
                            "Check Credentials And Try Again"
                        )
                    };

                byte[] salt = RandomNumberGenerator.GetBytes(16);
                record.Server.PasswordSalt = ByteString.CopyFrom(salt);
                record.Server.PasswordHash = ByteString.CopyFrom(
                    ComputeSaltedHash(request.NewPassword, salt)
                );

                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new ChangeOwnPasswordResponse
                {
                    Error = GenericErrorExtensions.CreateNoError() // Success case - no error
                };
            }
            catch
            {
                return new ChangeOwnPasswordResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonUnknown,
                        "An unexpected error occurred while changing password"
                    )
                };
            }
        }

        public override async Task<ChangeOwnProfileImageResponse> ChangeOwnProfileImage(
            ChangeOwnProfileImageRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new ChangeOwnProfileImageResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonServiceUnavailable,
                        "Service is offline"
                    )
                };

            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new ChangeOwnProfileImageResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonUnauthenticated,
                            "User authentication required"
                        )
                    };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new ChangeOwnProfileImageResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonNotFound,
                            "User record not found"
                        )
                    };

                if (request?.ProfileImage == null || request.ProfileImage.IsEmpty)
                    return new ChangeOwnProfileImageResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonInvalidContent,
                            "Profile image data is required"
                        )
                    };

                using var ms = new MemoryStream();
                ms.Write(request.ProfileImage.ToArray());
                ms.Position = 0;
                using var image = SKBitmap.Decode(ms);

                if (image == null)
                    return new ChangeOwnProfileImageResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonInvalidContent,
                            "Invalid image format"
                        )
                    };

                var newInfo = image.Info;
                newInfo.Width = 200;
                newInfo.Height = 200;
                using var newImage = image.Resize(newInfo, SKFilterQuality.Medium);

                using MemoryStream memStream = new MemoryStream();
                using SKManagedWStream wstream = new SKManagedWStream(memStream);

                newImage.Encode(wstream, SKEncodedImageFormat.Png, 50);

                await picProvider.Save(userToken.Id, memStream.ToArray());

                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new ChangeOwnProfileImageResponse
                {
                    Error = GenericErrorExtensions.CreateNoError() // Success case - no error
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in ChangeOwnProfileImage");
                return new ChangeOwnProfileImageResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonUnknown,
                        "An unexpected error occurred while processing the image"
                    )
                };
            }
        }

        [AllowAnonymous]
        public override async Task<CreateUserResponse> CreateUser(
            CreateUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new CreateUserResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonServiceUnavailable,
                        "Service is offline"
                    )
                };

            if (request is null)
                return new CreateUserResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonInvalidContent,
                        "Request was null"
                    )
                };

            var validator = new ProtoValidate.Validator();

            // NOTE: some builds expose (request), others (request, bool). Use the 2-arg call here.
            var validationResult = validator.Validate(request, false);
            if (validationResult.Violations.Count > 0)
            {
                // Use the enhanced extension method to convert ProtoValidate results
                var validationError = GenericErrorExtensions.FromProtoValidateResult(
                    validationResult,
                    APIErrorReason.ErrorReasonValidationFailed,
                    "Validation failed"
                );

                return new CreateUserResponse { Error = validationError };
            }

            var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
            var newGuid = Guid.NewGuid();
            var now = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

            var user = new UserRecord
            {
                Normal = new()
                {
                    Public = new()
                    {
                        UserID = newGuid.ToString(),
                        CreatedOnUTC = now,
                        ModifiedOnUTC = now,
                        Data = new()
                        {
                            UserName = (request.UserName ?? string.Empty).ToLowerInvariant(),
                            DisplayName = request.DisplayName ?? string.Empty,
                            Bio = request.Bio ?? string.Empty,
                        },
                    },
                    Private = new()
                    {
                        CreatedBy = (userToken?.Id ?? newGuid).ToString(),
                        ModifiedBy = (userToken?.Id ?? newGuid).ToString(),
                        Data = new()
                        {
                            Email = request.Email ?? string.Empty,
                            FirstName = request.FirstName ?? string.Empty,
                            LastName = request.LastName ?? string.Empty,
                            PostalCode = request.PostalCode ?? string.Empty,
                        },
                    },
                },
                Server = new(),
            };

            byte[] salt = RandomNumberGenerator.GetBytes(16);
            user.Server.PasswordSalt = Google.Protobuf.ByteString.CopyFrom(salt);
            user.Server.PasswordHash = Google.Protobuf.ByteString.CopyFrom(
                ComputeSaltedHash(request.Password ?? string.Empty, salt)
            );

            var uname = user.Normal.Public.Data.UserName;
            if (await dataProvider.LoginExists(uname))
                return new CreateUserResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonAlreadyExists,
                        "Username is already taken"
                    )
                };

            var email = user.Normal.Private.Data.Email;
            if (await dataProvider.EmailExists(email))
                return new CreateUserResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonAlreadyExists,
                        "Email is already taken"
                    )
                };

            var ok = await dataProvider.Create(user);
            if (!ok)
                return new CreateUserResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonProviderError,
                        "Data provider failed to create user"
                    )
                };

            return new CreateUserResponse { BearerToken = tokenHelper.GenerateToken(user.Normal, null) };
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<DisableEnableOtherUserResponse> DisableOtherUser(
            DisableEnableOtherUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new DisableEnableOtherUserResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonServiceUnavailable,
                        "Service is currently offline"
                    )
                };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new DisableEnableOtherUserResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonUnauthorized,
                            "Admin access required"
                        )
                    };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new DisableEnableOtherUserResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonNotFound,
                            "User not found"
                        )
                    };

                record.Normal.Public.DisabledOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.DisabledBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new DisableEnableOtherUserResponse
                {
                    Error = GenericErrorExtensions.CreateNoError() // Success case - no error
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DisableOtherUser");
                return new DisableEnableOtherUserResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonUnknown,
                        "An unexpected error occurred while disabling user"
                    )
                };
            }
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<DisableOtherTotpResponse> DisableOtherTotp(
            DisableOtherTotpRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthorized, "Admin only") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthorized, "Not logged in") };

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "User not found") };

                var totp = record.Server.TOTPDevices.FirstOrDefault(r =>
                    r.TotpID == request.TotpID
                );
                if (totp == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Device not found") };

                totp.DisabledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new DisableOtherTotpResponse
                {
                    Error = GenericErrorExtensions.CreateNoError()
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DisableOtherTotp");
                return new DisableOtherTotpResponse
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Error Disabling Other User TOTP Device")
                };
            }
        }

        public override async Task<DisableOwnTotpResponse> DisableOwnTotp(
            DisableOwnTotpRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not logged in") };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not logged in") };

                var totp = record.Server.TOTPDevices.FirstOrDefault(r =>
                    r.TotpID == request.TotpID
                );
                if (totp == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Device not found") };

                totp.DisabledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new()
                {
                    Error = GenericErrorExtensions.CreateNoError()
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DisableOwnTotp");
                return new DisableOwnTotpResponse
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Error While Disabling Own TOTP Device")
                };
            }
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<DisableEnableOtherUserResponse> EnableOtherUser(
            DisableEnableOtherUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new DisableEnableOtherUserResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonServiceUnavailable,
                        "Service is currently offline"
                    )
                };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new DisableEnableOtherUserResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonUnauthorized,
                            "Admin access required"
                        )
                    };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new DisableEnableOtherUserResponse
                    {
                        Error = GenericErrorExtensions.CreateError(
                            APIErrorReason.ErrorReasonNotFound,
                            "User not found"
                        )
                    };

                record.Normal.Public.DisabledOnUTC = null;
                record.Normal.Private.DisabledBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new DisableEnableOtherUserResponse
                {
                    Error = GenericErrorExtensions.CreateNoError() // Success case - no error
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in EnableOtherUser");
                return new DisableEnableOtherUserResponse
                {
                    Error = GenericErrorExtensions.CreateError(
                        APIErrorReason.ErrorReasonUnknown,
                        "An unexpected error occurred while enabling user"
                    )
                };
            }
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<GenerateOtherTotpResponse> GenerateOtherTotp(
            GenerateOtherTotpRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonServiceUnavailable, "Offline") };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthorized, "Admin only") };

                var deviceName = request.DeviceName?.Trim();
                if (string.IsNullOrWhiteSpace(deviceName))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Device Name required") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthorized, "Not logged in") };

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "User not found") };

                if (
                    record
                        .Server.TOTPDevices.Where(r => r.IsValid)
                        .Where(r => r.DeviceName.ToLower() == deviceName.ToLower())
                        .Any()
                )
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonAlreadyExists, "Device Name already exists") };

                byte[] key = new byte[10];
                rng.GetBytes(key);

                TOTPDevice totp = new()
                {
                    TotpID = Guid.NewGuid().ToString(),
                    DeviceName = deviceName,
                    Key = ByteString.CopyFrom(key),
                    CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                        DateTime.UtcNow
                    ),
                };

                record.Server.TOTPDevices.Add(totp);

                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                var settingsData = await settingsService.GetAdminDataInternal();

                TwoFactorAuthenticator tfa = new TwoFactorAuthenticator();
                SetupCode setupInfo = tfa.GenerateSetupCode(
                    settingsData.Public.Personalization.Title,
                    record.Normal.Public.Data.UserName,
                    key
                );

                return new()
                {
                    TotpID = totp.TotpID,
                    Key = setupInfo.ManualEntryKey,
                    QRCode = setupInfo.QrCodeSetupImageUrl,
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in GenerateOtherTotp");
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown Error") };
            }
        }

        public override async Task<GenerateOwnTotpResponse> GenerateOwnTotp(
            GenerateOwnTotpRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonServiceUnavailable, "Offline") };

            try
            {
                var deviceName = request.DeviceName?.Trim();
                if (string.IsNullOrWhiteSpace(deviceName))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Device Name required") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not logged in") };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not logged in") };

                if (
                    record
                        .Server.TOTPDevices.Where(r => r.IsValid)
                        .Where(r => r.DeviceName.ToLower() == deviceName.ToLower())
                        .Any()
                )
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonAlreadyExists, "Device Name already exists") };

                byte[] key = new byte[10];
                rng.GetBytes(key);

                TOTPDevice totp = new()
                {
                    TotpID = Guid.NewGuid().ToString(),
                    DeviceName = deviceName,
                    Key = ByteString.CopyFrom(key),
                    CreatedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                        DateTime.UtcNow
                    ),
                };

                record.Server.TOTPDevices.Add(totp);

                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                var settingsData = await settingsService.GetAdminDataInternal();

                TwoFactorAuthenticator tfa = new TwoFactorAuthenticator();
                SetupCode setupInfo = tfa.GenerateSetupCode(
                    settingsData.Public.Personalization.Title,
                    record.Normal.Public.Data.UserName,
                    key
                );

                return new()
                {
                    TotpID = totp.TotpID,
                    Key = setupInfo.ManualEntryKey,
                    QRCode = setupInfo.QrCodeSetupImageUrl,
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in GenerateOwnTotp");
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown Error") };
            }
        }

        [Authorize(Roles = ONUser.ROLE_IS_MEMBER_MANAGER_OR_HIGHER)]
        public override async Task<GetAllUsersResponse> GetAllUsers(
            GetAllUsersRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            List<UserNormalRecord> list = new();

            var ret = new GetAllUsersResponse();
            try
            {
                if (!await AmIReallyAdmin(context))
                    return ret;
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                await foreach (var r in dataProvider.GetAll())
                    list.Add(r.Normal);
            }
            catch { }

            ret.Records.AddRange(list.OrderByDescending(r => r.Public.Data.UserName));
            ret.PageTotalItems = (uint)ret.Records.Count;

            if (request.PageSize > 0)
            {
                var page = ret
                    .Records.Skip((int)request.PageOffset)
                    .Take((int)request.PageSize)
                    .ToList();
                ret.Records.Clear();
                ret.Records.AddRange(page);
            }

            ret.PageOffsetStart = request.PageOffset;
            ret.PageOffsetEnd = ret.PageOffsetStart + (uint)ret.Records.Count;

            return ret;
        }

        [Authorize(Roles = ONUser.ROLE_IS_MEMBER_MANAGER_OR_HIGHER)]
        public override async Task GetListOfOldUserIDs(
            GetListOfOldUserIDsRequest request,
            IServerStreamWriter<GetListOfOldUserIDsResponse> responseStream,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return;

            if (!await AmIReallyAdmin(context))
                return;

            try
            {
                await foreach (var r in dataProvider.GetAll())
                {
                    if (r.Normal.Private.Data.OldUserID != "")
                        await responseStream.WriteAsync(
                            new()
                            {
                                UserID = r.Normal.Public.UserID,
                                OldUserID = r.Normal.Private.Data.OldUserID,
                                ModifiedOnUTC = r.Normal.Public.ModifiedOnUTC,
                            }
                        );
                }
            }
            catch { }
        }

        [Authorize(Roles = ONUser.ROLE_IS_MEMBER_MANAGER_OR_HIGHER)]
        public override async Task<GetOtherUserResponse> GetOtherUser(
            GetOtherUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            var id = request.UserID.ToGuid();

            var record = await dataProvider.GetById(id);
            await userServiceInternal.AddInProfilePic(record);

            return new() { Record = record?.Normal };
        }

        [AllowAnonymous]
        public override Task<GetOtherPublicUserResponse> GetOtherPublicUser(
            GetOtherPublicUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return Task.FromResult(new GetOtherPublicUserResponse());

            return userServiceInternal.GetOtherPublicUserInternal(request.UserID.ToGuid());
        }

        [AllowAnonymous]
        public override async Task<GetOtherPublicUserByUserNameResponse> GetOtherPublicUserByUserName(
            GetOtherPublicUserByUserNameRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            var record = await dataProvider.GetByLogin(request.UserName);
            await userServiceInternal.AddInProfilePic(record);

            return new() { Record = record?.Normal.Public };
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<GetOtherTotpListResponse> GetOtherTotpList(
            GetOtherTotpListRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new();

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new();

                var ret = new GetOtherTotpListResponse();
                ret.Devices.AddRange(
                    record.Server.TOTPDevices.Where(r => r.IsValid).Select(r => r.ToLimited())
                );

                return ret;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in GetOtherTotpList");
                return new();
            }
        }

        public override async Task<GetOwnTotpListResponse> GetOwnTotpList(
            GetOwnTotpListRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new();

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new();

                var ret = new GetOwnTotpListResponse();
                ret.Devices.AddRange(
                    record.Server.TOTPDevices.Where(r => r.IsValid).Select(r => r.ToLimited())
                );

                return ret;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in GetOwnTotpList");
                return new();
            }
        }

        public override async Task<GetOwnUserResponse> GetOwnUser(
            GetOwnUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
            if (userToken == null)
                return new();

            var record = await dataProvider.GetById(userToken.Id);
            await userServiceInternal.AddInProfilePic(record);

            return new() { Record = record?.Normal };
        }

        [AllowAnonymous]
        public override Task<GetUserIdListResponse> GetUserIdList(
            GetUserIdListRequest request,
            ServerCallContext context
        )
        {
            return userServiceInternal.GetUserIdListInternal();
        }

        [Authorize(Roles = ONUser.ROLE_IS_MEMBER_MANAGER_OR_HIGHER)]
        public override async Task<ModifyOtherUserResponse> ModifyOtherUser(
            ModifyOtherUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonServiceUnavailable, "Service Offline") };

            try
            {
                //if (!await AmIReallyAdmin(context))
                //    return new() { Error = ErrorExtensions.CreateError(APIErrorReason.ModifyOtherUserErrorUnauthorized, "Not an admin") };
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var userId = request.UserID.ToGuid();
                var record = await dataProvider.GetById(userId);
                if (record == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "User not found") };

                if (!IsUserNameValid(request.UserName))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "User Name not valid") };

                request.UserName = request.UserName.ToLower();

                if (!IsDisplayNameValid(request.DisplayName))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Display Name not valid") };

                if (record.Normal.Public.Data.UserName != request.UserName)
                {
                    if (
                        !await dataProvider.ChangeLoginIndex(
                            record.Normal.Public.Data.UserName,
                            request.UserName,
                            userId
                        )
                    )
                        return new ModifyOtherUserResponse() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonAlreadyExists, "User Name taken") };

                    record.Normal.Public.Data.UserName = request.UserName;
                }

                if (record.Normal.Private.Data.Email != request.Email)
                {
                    if (!await dataProvider.ChangeEmailIndex(request.Email, userId))
                        return new ModifyOtherUserResponse() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonAlreadyExists, "Email address taken") };

                    record.Normal.Private.Data.Email = request.Email;
                }

                record.Normal.Private.Data.FirstName = request.FirstName;
                record.Normal.Private.Data.LastName = request.LastName;
                record.Normal.Private.Data.PostalCode = request.PostalCode;

                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Public.Data.DisplayName = request.DisplayName;
                record.Normal.Public.Data.Bio = request.Bio;

                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new();
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error") };
            }
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyOtherUserRolesResponse> ModifyOtherUserRoles(
            ModifyOtherUserRolesRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonServiceUnavailable, "Service Offline") };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not an admin") };
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (!userToken.CanManageMembers)
                {
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not an admin") };
                }
                var userId = request.UserID.ToGuid();
                var record = await dataProvider.GetById(userId);
                if (record == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "User not found") };

                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

                record.Normal.Private.ModifiedBy = userToken.Id.ToString();
                record.Normal.Private.Roles.Clear();
                record.Normal.Private.Roles.AddRange(request.Roles);

                await dataProvider.Save(record);

                return new();
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error") };
            }
        }

        public override async Task<ModifyOwnUserResponse> ModifyOwnUser(
            ModifyOwnUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonServiceUnavailable, "Service Offline") };

            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "No user token specified") };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "User not found") };

                if (!IsDisplayNameValid(request.DisplayName))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Display Name not valid") };

                record.Normal.Public.Data.DisplayName = request.DisplayName;
                record.Normal.Public.Data.Bio = request.Bio;

                if (record.Normal.Private.Data.Email != request.Email)
                {
                    if (!await dataProvider.ChangeEmailIndex(request.Email, record.UserIDGuid))
                        return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonAlreadyExists, "Email address taken") };

                    record.Normal.Private.Data.Email = request.Email;
                }

                record.Normal.Private.Data.FirstName = request.FirstName;
                record.Normal.Private.Data.LastName = request.LastName;
                record.Normal.Private.Data.PostalCode = request.PostalCode;

                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                var otherClaims = await claimsClient.GetOtherClaims(userToken.Id);

                return new() { BearerToken = tokenHelper.GenerateToken(record.Normal, otherClaims) };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error") };
            }
        }

        public override async Task<RenewTokenResponse> RenewToken(
            RenewTokenRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new();

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new();

                var otherClaims = await claimsClient.GetOtherClaims(userToken.Id);

                return new() { BearerToken = tokenHelper.GenerateToken(record.Normal, otherClaims) };
            }
            catch
            {
                return new();
            }
        }

        [Authorize(Roles = ONUser.ROLE_IS_MEMBER_MANAGER_OR_HIGHER)]
        public override async Task<SearchUsersAdminResponse> SearchUsersAdmin(
            SearchUsersAdminRequest request,
            ServerCallContext context
        )
        {
            var minDateValue = new DateTime(2000, 1, 1);

            var possibleIDs = request.UserIDs.ToList();
            var possibleRoles = request.Roles.ToList();
            var searchSearchString = request.SearchString;
            var searchCreatedBefore = request.CreatedBefore;
            var searchCreatedAfter = request.CreatedAfter;
            var searchIncludeDeleted = request.IncludeDeleted;

            if (!possibleIDs.Any())
                possibleIDs = null;
            if (!possibleRoles.Any())
                possibleRoles = null;
            if (string.IsNullOrWhiteSpace(searchSearchString))
                searchSearchString = null;

            var res = new SearchUsersAdminResponse();

            List<UserSearchRecord> list = new();
            await foreach (var rec in dataProvider.GetAll())
            {
                if (possibleIDs != null)
                    if (!possibleIDs.Contains(rec.Normal.Public.UserID))
                        continue;

                if (possibleRoles != null)

                    if (!possibleRoles.Any(possibleRole =>
                        rec.Normal.Private.Roles.Any(role => string.Equals(possibleRole, role, StringComparison.InvariantCultureIgnoreCase))
                    ))
                        continue;

                if (searchCreatedBefore != null)
                    if (rec.Normal.Public.CreatedOnUTC < searchCreatedBefore)
                        continue;

                if (searchCreatedAfter != null)
                    if (rec.Normal.Public.CreatedOnUTC > searchCreatedAfter)
                        continue;

                if (!searchIncludeDeleted)
                    if (rec.Normal.Public.DisabledOnUTC != null)
                        continue;

                if (searchSearchString != null)
                    if (
                        !rec.Normal.Public.Data.UserName.Contains(
                            searchSearchString,
                            StringComparison.InvariantCultureIgnoreCase
                        )
                        && !rec.Normal.Public.Data.DisplayName.Contains(
                            searchSearchString,
                            StringComparison.InvariantCultureIgnoreCase
                        )
                    )
                        continue;

                var listRec = rec.Normal.ToUserSearchRecord();

                list.Add(listRec);
            }

            res.Records.AddRange(list.OrderBy(r => r.DisplayName));
            res.PageTotalItems = (uint)res.Records.Count;

            if (request.PageSize > 0)
            {
                res.PageOffsetStart = request.PageOffset;

                var page = res
                    .Records.Skip((int)request.PageOffset)
                    .Take((int)request.PageSize)
                    .ToList();
                res.Records.Clear();
                res.Records.AddRange(page);
            }

            res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;

            return res;
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<VerifyOtherTotpResponse> VerifyOtherTotp(
            VerifyOtherTotpRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthorized, "Admin only") };

                if (string.IsNullOrWhiteSpace(request?.Code))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Code is required") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not logged in") };

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "User not found") };

                var totp = record.Server.TOTPDevices.FirstOrDefault(r =>
                    r.TotpID == request.TotpID
                );
                if (totp == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Device not found") };

                TwoFactorAuthenticator tfa = new TwoFactorAuthenticator();
                if (!tfa.ValidateTwoFactorPIN(totp.Key.ToByteArray(), request.Code.Trim()))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidCode, "Code is not valid") };

                totp.VerifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in VerifyOtherTotp");
                return new();
            }
        }

        public override async Task<VerifyOwnTotpResponse> VerifyOwnTotp(
            VerifyOwnTotpRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            try
            {
                if (string.IsNullOrWhiteSpace(request?.Code))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Code is required") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not logged in") };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not logged in") };

                var totp = record.Server.TOTPDevices.FirstOrDefault(r =>
                    r.TotpID == request.TotpID
                );
                if (totp == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Device not found") };

                TwoFactorAuthenticator tfa = new TwoFactorAuthenticator();
                if (!tfa.ValidateTwoFactorPIN(totp.Key.ToByteArray(), request.Code.Trim()))
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Code is not valid") };

                totp.VerifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in VerifyOwnTotp");
                return new();
            }
        }

        // TODO: Add New Roles Or New Method To Support New Roles
        private async Task<bool> AmIReallyAdmin(ServerCallContext context)
        {
            var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
            if (userToken == null)
                return false;

            var record = await dataProvider.GetById(userToken.Id);
            if (record == null)
                return false;

            var roles = record.Normal.Private.Roles;
            if (!(roles.Contains(ONUser.ROLE_OWNER) || roles.Contains(ONUser.ROLE_ADMIN)))
                return false;

            return true;
        }

        private async Task<bool> IsPasswordCorrect(string password, UserRecord user)
        {
            var hash = ComputeSaltedHash(password, user.Server.PasswordSalt.Span);
            if (CryptographicOperations.FixedTimeEquals(user.Server.PasswordHash.Span, hash))
                return true;

            if (
                string.IsNullOrEmpty(user.Server.OldPasswordAlgorithm)
                || string.IsNullOrEmpty(user.Server.OldPassword)
            )
                return false;

            if (user.Server.OldPasswordAlgorithm == "Wordpress")
            {
                if (!CryptSharp.Core.PhpassCrypter.CheckPassword(password, user.Server.OldPassword))
                    return false;

                byte[] salt = RandomNumberGenerator.GetBytes(16);
                user.Server.PasswordSalt = ByteString.CopyFrom(salt);
                user.Server.PasswordHash = ByteString.CopyFrom(ComputeSaltedHash(password, salt));

                user.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                user.Normal.Private.ModifiedBy = user.Normal.Public.UserID;

                await dataProvider.Save(user);

                return true;
            }

            return false;
        }

        private bool IsValid(UserNormalRecord user)
        {
            if (user.Public.UserID.ToGuid() == Guid.Empty)
                return false;

            user.Public.Data.DisplayName = user.Public.Data.DisplayName?.Trim() ?? "";
            if (!IsDisplayNameValid(user.Public.Data.DisplayName))
                return false;

            user.Public.Data.UserName = user.Public.Data.UserName?.Trim() ?? "";
            if (!IsUserNameValid(user.Public.Data.UserName))
                return false;

            return true;
        }

        private bool IsDisplayNameValid(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return false;

            if (displayName.Length < 4 || displayName.Length > 20)
                return false;

            return true;
        }

        private bool IsUserNameValid(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return false;

            if (userName.Length < 4 || userName.Length > 20)
                return false;

            var regex = new Regex(@"^[a-z0-9]+$");
            if (!regex.IsMatch(userName))
                return false;

            return true;
        }

        private byte[] ComputeSaltedHash(string plainText, ReadOnlySpan<byte> salt)
        {
            return ComputeSaltedHash(Encoding.UTF8.GetBytes(plainText), salt);
        }

        private byte[] ComputeSaltedHash(ReadOnlySpan<byte> plainText, ReadOnlySpan<byte> salt)
        {
            byte[] plainTextWithSaltBytes = new byte[plainText.Length + salt.Length];

            plainText.CopyTo(plainTextWithSaltBytes.AsSpan());
            salt.CopyTo(plainTextWithSaltBytes.AsSpan(plainText.Length));

            return hasher.ComputeHash(plainTextWithSaltBytes);
        }

        private bool ValidateTotp(IEnumerable<TOTPDevice> devices, string code)
        {
            var validDevices = devices?.Where(d => d.IsValid) ?? Enumerable.Empty<TOTPDevice>();

            // If there are no TOTP Devices then don't require one
            if (!validDevices.Any())
                return true;

            code = code?.Trim() ?? "";

            foreach (var device in validDevices)
                if (IsValidTotp(device, code))
                    return true;

            return false;
        }

        private bool IsValidTotp(TOTPDevice device, string code)
        {
            TwoFactorAuthenticator tfa = new TwoFactorAuthenticator();
            return tfa.ValidateTwoFactorPIN(device.Key.ToByteArray(), code);
        }

        private async Task EnsureDevOwnerLogin()
        {
            if (await dataProvider.LoginExists("owner"))
                return;

            var date = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
            var newId = Guid.NewGuid().ToString();

            var record = new UserRecord()
            {
                Normal = new()
                {
                    Public = new()
                    {
                        UserID = newId,
                        CreatedOnUTC = date,
                        ModifiedOnUTC = date,
                        Data = new() { UserName = "owner", DisplayName = "Owner" },
                    },
                    Private = new()
                    {
                        CreatedBy = newId,
                        ModifiedBy = newId,
                        Data = new(),
                    },
                },
                Server = new(),
            };

            record.Normal.Private.Roles.Add(ONUser.ROLE_OWNER);

            byte[] salt = RandomNumberGenerator.GetBytes(16);
            record.Server.PasswordSalt = ByteString.CopyFrom(salt);
            record.Server.PasswordHash = ByteString.CopyFrom(ComputeSaltedHash("owner", salt));

            await dataProvider.Create(record);
        }
    }
}
