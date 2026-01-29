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
        private readonly SigningCredentials creds;
        private readonly IProfilePicDataProvider picProvider;
        private readonly IUserDataProvider dataProvider;
        private readonly ClaimsClient claimsClient;
        private readonly ISettingsService settingsService;
        private readonly UserServiceInternal userServiceInternal;
        private static readonly HashAlgorithm hasher = SHA256.Create();
        private static readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();

        public UserService(OfflineHelper offlineHelper, ILogger<UserService> logger, IProfilePicDataProvider picProvider, IUserDataProvider dataProvider, ClaimsClient claimsClient, ISettingsService settingsService, UserServiceInternal userServiceInternal)
        {
            this.offlineHelper = offlineHelper;
            this.logger = logger;
            this.picProvider = picProvider;
            this.dataProvider = dataProvider;
            this.claimsClient = claimsClient;
            this.settingsService = settingsService;
            this.userServiceInternal = userServiceInternal;

            creds = new SigningCredentials(
                JwtExtensions.GetPrivateKey(),
                SecurityAlgorithms.EcdsaSha256
            );

            //if (Program.IsDevelopment)
            //{
            EnsureDevOwnerLogin().Wait();
            //}
        }

        [AllowAnonymous]
        public override async Task<AuthenticateUserResponse> AuthenticateUser(
            AuthenticateUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new AuthenticateUserResponse{
                    Ok = false,
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.LoginErrorServiceUnavailable,
                        "Server Unavailable, Try Again Later"
                    )
                };

            var validationError = ErrorExtensions.CreateError(
                AuthErrorReason.LoginErrorInvalidCredentials,
                "Invalid credentials provided"
            );

            bool hasValidationErrors = false;

            if (string.IsNullOrWhiteSpace(request.UserName))
            {
                validationError.AddValidationIssue("UserName", "Username is required", "required");
                hasValidationErrors = true;
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                validationError.AddValidationIssue("Password", "Password is required", "required");
                hasValidationErrors = true;
            }

            if (hasValidationErrors)
                return new AuthenticateUserResponse
                {
                    Ok = false,
                    Error = validationError
                };

            var user = await dataProvider.GetByLogin(request.UserName);
            if (user == null)
            {
                user = await dataProvider.GetByEmail(request.UserName);
                if (user == null)
                    return new AuthenticateUserResponse
                        {
                            Ok = false,
                            Error = ErrorExtensions.CreateError(
                                AuthErrorReason.LoginErrorInvalidCredentials,
                                "User Not Found"
                            ).AddValidationIssue("UserName", "User not found with provided username or email", "not_found")
                    };
            }

            bool isCorrect = await IsPasswordCorrect(request.Password, user);

            if (!isCorrect)
                return new AuthenticateUserResponse
                {
                    Ok = false,
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.LoginErrorInvalidCredentials,
                        "Check Credentials and try Again"
                    ).AddValidationIssue("Password", "Invalid password provided", "invalid")
                };

            if (!ValidateTotp(user.Server?.TOTPDevices, request.MFACode))
                return new AuthenticateUserResponse
                {
                    Ok = false,
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.LoginErrorInvalidMfaCode,
                        "MFACode Invalid"
                    ).AddValidationIssue("MFACode", "Invalid MFA code provided", "invalid")
                };

            var otherClaims = await claimsClient.GetOtherClaims(user.UserIDGuid);

            return new AuthenticateUserResponse()
            {
                Ok = true,
                BearerToken = GenerateToken(user.Normal, otherClaims),
                UserRecord = user.Normal,
            };
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ChangeOtherPasswordResponse> ChangeOtherPassword(
            ChangeOtherPasswordRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new ChangeOtherPasswordResponse
                {
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.ChangeOtherPasswordErrorUnknown,
                        "Service is offline"
                    )
                };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new ChangeOtherPasswordResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOtherPasswordErrorUnknown,
                            "Admin access required"
                        )
                    };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new ChangeOtherPasswordResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOtherPasswordErrorUserNotFound,
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
                    Error = null // Success case - no error
                };
            }
            catch
            {
                return new ChangeOtherPasswordResponse
                {
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.ChangeOtherPasswordErrorUnknown,
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
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.ChangeOtherProfileImageErrorUnknown,
                        "Service is offline"
                    )
                };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new ChangeOtherProfileImageResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOtherProfileImageErrorUnknown,
                            "Admin access required"
                        )
                    };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new ChangeOtherProfileImageResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOtherProfileImageErrorUserNotFound,
                            "User not found"
                        )
                    };

                if (request?.ProfileImage == null || request.ProfileImage.IsEmpty)
                    return new ChangeOtherProfileImageResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOtherProfileImageErrorBadFormat,
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
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOtherProfileImageErrorBadFormat,
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
                    Error = null // Success case - no error
                };
            }
            catch
            {
                return new ChangeOtherProfileImageResponse
                {
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.ChangeOtherProfileImageErrorUnknown,
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
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.ChangeOwnPasswordErrorUnknown,
                        "Service is offline"
                    )
                };

            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new ChangeOwnPasswordResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOwnPasswordErrorUnknown,
                            "User authentication required"
                        )
                    };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new ChangeOwnPasswordResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOwnPasswordErrorUnknown,
                            "User record not found"
                        )
                    };

                var hash = ComputeSaltedHash(request.OldPassword, record.Server.PasswordSalt.Span);
                if (!CryptographicOperations.FixedTimeEquals(record.Server.PasswordHash.Span, hash))
                    return new ChangeOwnPasswordResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOwnPasswordErrorBadOldPassword,
                            "Current password is incorrect"
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
                    Error = null // Success case - no error
                };
            }
            catch
            {
                return new ChangeOwnPasswordResponse
                {
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.ChangeOwnPasswordErrorUnknown,
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
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.ChangeOwnProfileImageErrorUnknown,
                        "Service is offline"
                    )
                };

            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new ChangeOwnProfileImageResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOwnProfileImageErrorUnknown,
                            "User authentication required"
                        )
                    };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new ChangeOwnProfileImageResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOwnProfileImageErrorUnknown,
                            "User record not found"
                        )
                    };

                if (request?.ProfileImage == null || request.ProfileImage.IsEmpty)
                    return new ChangeOwnProfileImageResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOwnProfileImageErrorBadFormat,
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
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.ChangeOwnProfileImageErrorBadFormat,
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
                    Error = null // Success case - no error
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in ChangeOwnProfileImage");
                return new ChangeOwnProfileImageResponse
                {
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.ChangeOwnProfileImageErrorUnknown,
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
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.CreateUserErrorUnknown,
                        "Service is offline"
                    )
                };

            if (request is null)
                return new CreateUserResponse
                {
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.CreateUserErrorUnknown,
                        "Request was null"
                    ).AddValidationIssue("request", "Request cannot be null", "required")
                };

            var validator = new ProtoValidate.Validator();

            // NOTE: some builds expose (request), others (request, bool). Use the 2-arg call here.
            var validationResult = validator.Validate(request, false);
            if (validationResult.Violations.Count > 0)
            {
                // Use the enhanced extension method to convert ProtoValidate results
                var validationError = ErrorExtensions.FromProtoValidateResult(
                    validationResult,
                    AuthErrorReason.CreateUserErrorUnknown,
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
                        Data = new() { Email = request.Email ?? string.Empty },
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
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.CreateUserErrorUsernameTaken,
                        "Username is already taken"
                    ).AddValidationIssue("UserName", "Username is already taken", "unique")
                };

            var email = user.Normal.Private.Data.Email;
            if (await dataProvider.EmailExists(email))
                return new CreateUserResponse
                {
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.CreateUserErrorEmailTaken,
                        "Email is already taken"
                    ).AddValidationIssue("Email", "Email is already taken", "unique")
                };

            var ok = await dataProvider.Create(user);
            if (!ok)
                return new CreateUserResponse
                {
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.CreateUserErrorUnknown,
                        "Failed to create user"
                    )
                };

            return new CreateUserResponse { BearerToken = GenerateToken(user.Normal, null) };
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
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.DisableOtherUserErrorUnknown,
                        "Service is currently offline"
                    )
                };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new DisableEnableOtherUserResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.DisableOtherUserErrorUnknown,
                            "Admin access required"
                        )
                    };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new DisableEnableOtherUserResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.DisableOtherUserErrorUnknown,
                            "User not found"
                        )
                    };

                record.Normal.Public.DisabledOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.DisabledBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new DisableEnableOtherUserResponse
                {
                    Error = null // Success case - no error
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DisableOtherUser");
                return new DisableEnableOtherUserResponse
                {
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.DisableOtherUserErrorUnknown,
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
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.DisableOtherTotpErrorUnknown, "Admin only") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.DisableOtherTotpErrorUnknown, "Not logged in") };

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.DisableOtherTotpErrorUnknown, "User not found") };

                var totp = record.Server.TOTPDevices.FirstOrDefault(r =>
                    r.TotpID == request.TotpID
                );
                if (totp == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.DisableOtherTotpErrorUnknown, "Device not found") };

                totp.DisabledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
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
                logger.LogError(ex, "Error in DisableOtherTotp");
                return new();
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
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.DisableOwnTotpErrorUnknown, "Not logged in") };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.DisableOwnTotpErrorUnknown, "Not logged in") };

                var totp = record.Server.TOTPDevices.FirstOrDefault(r =>
                    r.TotpID == request.TotpID
                );
                if (totp == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.DisableOwnTotpErrorUnknown, "Device not found") };

                totp.DisabledOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
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
                logger.LogError(ex, "Error in DisableOwnTotp");
                return new();
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
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.EnableOtherUserErrorUnknown,
                        "Service is currently offline"
                    )
                };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new DisableEnableOtherUserResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.EnableOtherUserErrorUnknown,
                            "Admin access required"
                        )
                    };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new DisableEnableOtherUserResponse
                    {
                        Error = ErrorExtensions.CreateError(
                            AuthErrorReason.EnableOtherUserErrorUnknown,
                            "User not found"
                        )
                    };

                record.Normal.Public.DisabledOnUTC = null;
                record.Normal.Private.DisabledBy = userToken.Id.ToString();

                await dataProvider.Save(record);

                return new DisableEnableOtherUserResponse
                {
                    Error = null // Success case - no error
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in EnableOtherUser");
                return new DisableEnableOtherUserResponse
                {
                    Error = ErrorExtensions.CreateError(
                        AuthErrorReason.EnableOtherUserErrorUnknown,
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
                return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOtherTotpErrorUnknown, "Offline") };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOtherTotpErrorUnknown, "Admin only") };

                var deviceName = request.DeviceName?.Trim();
                if (string.IsNullOrWhiteSpace(deviceName))
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOtherTotpErrorUnknown, "Device Name required") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOtherTotpErrorUnknown, "Not logged in") };

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOtherTotpErrorUnknown, "User not found") };

                if (
                    record
                        .Server.TOTPDevices.Where(r => r.IsValid)
                        .Where(r => r.DeviceName.ToLower() == deviceName.ToLower())
                        .Any()
                )
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOtherTotpErrorUnknown, "Device Name already exists") };

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
                return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOtherTotpErrorUnknown, "Unknown Error") };
            }
        }

        public override async Task<GenerateOwnTotpResponse> GenerateOwnTotp(
            GenerateOwnTotpRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOwnTotpErrorUnknown, "Offline") };

            try
            {
                var deviceName = request.DeviceName?.Trim();
                if (string.IsNullOrWhiteSpace(deviceName))
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOwnTotpErrorUnknown, "Device Name required") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOwnTotpErrorUnknown, "Not logged in") };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOwnTotpErrorUnknown, "Not logged in") };

                if (
                    record
                        .Server.TOTPDevices.Where(r => r.IsValid)
                        .Where(r => r.DeviceName.ToLower() == deviceName.ToLower())
                        .Any()
                )
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOwnTotpErrorUnknown, "Device Name already exists") };

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
                return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.GenerateOwnTotpErrorUnknown, "Unknown Error") };
            }
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
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

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
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

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<GetOtherUserResponse> GetOtherUser(
            GetOtherUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new();

            if (!await AmIReallyAdmin(context))
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

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyOtherUserResponse> ModifyOtherUser(
            ModifyOtherUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserErrorServiceOffline, "Service Offline") };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserErrorUnauthorized, "Not an admin") };
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var userId = request.UserID.ToGuid();
                var record = await dataProvider.GetById(userId);
                if (record == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserErrorUserNotFound, "User not found") };

                if (!IsUserNameValid(request.UserName))
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserErrorUnknown, "User Name not valid") };

                request.UserName = request.UserName.ToLower();

                if (!IsDisplayNameValid(request.DisplayName))
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserErrorUnknown, "Display Name not valid") };

                if (record.Normal.Public.Data.UserName != request.UserName)
                {
                    if (
                        !await dataProvider.ChangeLoginIndex(
                            record.Normal.Public.Data.UserName,
                            request.UserName,
                            userId
                        )
                    )
                        return new ModifyOtherUserResponse() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserErrorUsernameTaken, "User Name taken") };

                    record.Normal.Public.Data.UserName = request.UserName;
                }

                if (record.Normal.Private.Data.Email != request.Email)
                {
                    if (!await dataProvider.ChangeEmailIndex(request.Email, userId))
                        return new ModifyOtherUserResponse() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserErrorEmailTaken, "Email address taken") };

                    record.Normal.Private.Data.Email = request.Email;
                }

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
                return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserErrorUnknown, "Unknown error") };
            }
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyOtherUserRolesResponse> ModifyOtherUserRoles(
            ModifyOtherUserRolesRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserRolesErrorUnknown, "Service Offline") };

            try
            {
                if (!await AmIReallyAdmin(context))
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserRolesErrorUnknown, "Not an admin") };
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var userId = request.UserID.ToGuid();
                var record = await dataProvider.GetById(userId);
                if (record == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserRolesErrorUnknown, "User not found") };

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
                return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOtherUserRolesErrorUnknown, "Unknown error") };
            }
        }

        public override async Task<ModifyOwnUserResponse> ModifyOwnUser(
            ModifyOwnUserRequest request,
            ServerCallContext context
        )
        {
            if (offlineHelper.IsOffline)
                return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOwnUserErrorUnknown, "Service Offline") };

            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOwnUserErrorUnknown, "No user token specified") };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOwnUserErrorUnknown, "User not found") };

                if (!IsDisplayNameValid(request.DisplayName))
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOwnUserErrorUnknown, "Display Name not valid") };

                record.Normal.Public.Data.DisplayName = request.DisplayName;
                record.Normal.Public.Data.Bio = request.Bio;

                if (record.Normal.Private.Data.Email != request.Email)
                {
                    if (!await dataProvider.ChangeEmailIndex(request.Email, record.UserIDGuid))
                        return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOwnUserErrorUnknown, "Email address taken") };

                    record.Normal.Private.Data.Email = request.Email;
                }

                record.Normal.Public.ModifiedOnUTC =
                    Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                record.Normal.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                var otherClaims = await claimsClient.GetOtherClaims(userToken.Id);

                return new() { BearerToken = GenerateToken(record.Normal, otherClaims) };
            }
            catch
            {
                return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.ModifyOwnUserErrorUnknown, "Unknown error") };
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

                return new() { BearerToken = GenerateToken(record.Normal, otherClaims) };
            }
            catch
            {
                return new();
            }
        }

        [Authorize(Roles = ONUser.ROLE_IS_ADMIN_OR_OWNER)]
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
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.VerifyOtherTotpErrorUnknown, "Admin only") };

                if (string.IsNullOrWhiteSpace(request?.Code))
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.VerifyOtherTotpErrorInvalidCode, "Code is required") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.VerifyOtherTotpErrorUnknown, "Not logged in") };

                var record = await dataProvider.GetById(request.UserID.ToGuid());
                if (record == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.VerifyOtherTotpErrorUnknown, "User not found") };

                var totp = record.Server.TOTPDevices.FirstOrDefault(r =>
                    r.TotpID == request.TotpID
                );
                if (totp == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.VerifyOtherTotpErrorUnknown, "Device not found") };

                TwoFactorAuthenticator tfa = new TwoFactorAuthenticator();
                if (!tfa.ValidateTwoFactorPIN(totp.Key.ToByteArray(), request.Code.Trim()))
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.VerifyOtherTotpErrorInvalidCode, "Code is not valid") };

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
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.VerifyOwnTotpErrorInvalidCode, "Code is required") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.VerifyOwnTotpErrorUnknown, "Not logged in") };

                var record = await dataProvider.GetById(userToken.Id);
                if (record == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.VerifyOwnTotpErrorUnknown, "Not logged in") };

                var totp = record.Server.TOTPDevices.FirstOrDefault(r =>
                    r.TotpID == request.TotpID
                );
                if (totp == null)
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.VerifyOwnTotpErrorUnknown, "Device not found") };

                TwoFactorAuthenticator tfa = new TwoFactorAuthenticator();
                if (!tfa.ValidateTwoFactorPIN(totp.Key.ToByteArray(), request.Code.Trim()))
                    return new() { Error = ErrorExtensions.CreateError(AuthErrorReason.VerifyOwnTotpErrorInvalidCode, "Code is not valid") };

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

        private string GenerateToken(UserNormalRecord user, IEnumerable<ClaimRecord> otherClaims)
        {
            var onUser = new ONUser()
            {
                Id = user.Public.UserID.ToGuid(),
                UserName = user.Public.Data.UserName,
                DisplayName = user.Public.Data.DisplayName,
            };

            onUser.Idents.AddRange(user.Public.Data.Identities);
            onUser.Roles.AddRange(user.Private.Roles);

            if (otherClaims != null)
            {
                onUser.ExtraClaims.AddRange(otherClaims.Select(c => new Claim(c.Name, c.Value)));
                onUser.ExtraClaims.AddRange(
                    otherClaims.Select(c => new Claim(
                        c.Name + "Exp",
                        c.ExpiresOnUTC.Seconds.ToString()
                    ))
                );
            }

            return GenerateToken(onUser);
        }

        private string GenerateToken(ONUser user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var tokenExpiration = DateTime.UtcNow.AddDays(7);
            var claims = user.ToClaims().ToArray();
            var subject = new ClaimsIdentity(claims);
            var token = tokenHandler.CreateJwtSecurityToken(
                null,
                null,
                subject,
                null,
                tokenExpiration,
                DateTime.UtcNow,
                creds
            );

            return tokenHandler.WriteToken(token);
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
