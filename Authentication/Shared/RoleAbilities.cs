using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace IT.WebServices.Authentication
{
    public class RoleAbilities
    {
        public List<string> Roles { get; init; } = new List<string>();

        public RoleAbilities() { }
        public RoleAbilities(params string[] roles) => Roles.AddRange(roles);

        // Can do anything on the system
        public const string ROLE_OWNER = "owner";

        // Can do anything on the system except view/change sensitive payment settings or perform backups - only admins and owners can grant roles
        public const string ROLE_ADMIN = "admin";

        // Can access backup and restores
        public const string ROLE_BACKUP = "backup";

        // Can disable/enable services/website
        public const string ROLE_OPS = "ops";

        // Used for services to talk to outher services
        public const string ROLE_SERVICE = "service";

        // Can write or publish a piece of content
        public const string ROLE_CONTENT_PUBLISHER = "con_publisher";

        // Can write a piece of content, but cannot publish it
        public const string ROLE_CONTENT_WRITER = "con_writer";

        // Can moderate comments
        public const string ROLE_COMMENT_MODERATOR = "com_mod";

        // not implemented yet - Can manage comment moderation appeals
        public const string ROLE_COMMENT_APPELLATE_JUDGE = "com_appellate";

        // Allows bots limited access to certain elevated functions
        public const string ROLE_BOT_VERIFICATION = "bot_verification";

        // Can create/edit/delete events
        public const string ROLE_EVENT_MANAGER = "evt_manager";

        // Can assist users with issues with event ticket reservation issues
        public const string ROLE_EVENT_TICKET_MANAGER = "evt_tkt_manager";

        // Can assis user with membership issues: can names, email, perform password resets, and TOTP resets, etc... - Cannot manage any owners or admins
        public const string ROLE_MEMBER_MANAGER = "member_manager";

        // Can assis user with subscription issues: edit sub data, manually link subs, stop subs, change subs tiers, etc...
        public const string ROLE_SUBSCRIPTION_MANAGER = "sub_manager";

        public const string ROLE_CAN_BACKUP = ROLE_OWNER + "," + ROLE_BACKUP;
        public const string ROLE_CAN_CREATE_CONTENT = ROLE_CAN_PUBLISH + "," + ROLE_CONTENT_WRITER;
        public const string ROLE_CAN_MODERATE_COMMENT = ROLE_IS_COMMENT_MODERATOR_OR_HIGHER;
        public const string ROLE_CAN_PUBLISH = ROLE_IS_ADMIN_OR_OWNER + "," + ROLE_CONTENT_PUBLISHER;

        public const string ROLE_IS_ADMIN_OR_OWNER = ROLE_OWNER + "," + ROLE_ADMIN;
        public const string ROLE_IS_ADMIN_OR_OWNER_OR_SERVICE = ROLE_IS_ADMIN_OR_OWNER + "," + ROLE_SERVICE;
        public const string ROLE_IS_OWNER_OR_SERVICE = ROLE_SERVICE + "," + ROLE_OWNER;
        public const string ROLE_IS_ADMIN_OR_OWNER_OR_SERVICE_OR_BOT = ROLE_IS_ADMIN_OR_OWNER_OR_SERVICE + "," + ROLE_BOT_VERIFICATION;

        public const string ROLE_IS_COMMENT_MODERATOR_OR_HIGHER = ROLE_IS_COMMENT_APPELLATE_JUDGE_OR_HIGHER + "," + ROLE_COMMENT_MODERATOR;
        public const string ROLE_IS_COMMENT_APPELLATE_JUDGE_OR_HIGHER = ROLE_IS_ADMIN_OR_OWNER + "," + ROLE_COMMENT_APPELLATE_JUDGE;

        public const string ROLE_IS_EVENT_MANAGER_OR_HIGHER = ROLE_IS_ADMIN_OR_OWNER + "," + ROLE_EVENT_MANAGER;
        public const string ROLE_IS_EVENT_TICKET_MANAGER_OR_HIGHER = ROLE_IS_EVENT_MANAGER_OR_HIGHER + "," + ROLE_EVENT_TICKET_MANAGER;

        public const string ROLE_IS_MEMBER_MANAGER_OR_HIGHER = ROLE_MEMBER_MANAGER + "," + ROLE_IS_ADMIN_OR_OWNER;
        public const string ROLE_IS_SUBSCRIPTION_MANAGER_OR_HIGHER = ROLE_SUBSCRIPTION_MANAGER + "," + ROLE_IS_ADMIN_OR_OWNER;
        public const string ROLE_IS_SUBSCRIPTION_MANAGER_OR_HIGHER_OR_BOT = ROLE_SUBSCRIPTION_MANAGER + "," + ROLE_IS_ADMIN_OR_OWNER + "," + ROLE_BOT_VERIFICATION + "," + ROLE_SERVICE;

        public bool IsOwner => IsInRole(ROLE_OWNER);
        public bool IsAdmin => IsInRole(ROLE_ADMIN);
        public bool IsAdminOrHigher => IsAdmin || IsOwner;
        public bool IsBackup => IsInRole(ROLE_BACKUP);
        public bool IsBot => IsInRole(ROLE_BOT_VERIFICATION);

        public bool IsPublisher => IsInRole(ROLE_CONTENT_PUBLISHER);
        public bool IsPublisherOrHigher => IsPublisher || IsAdminOrHigher;
        public bool IsWriter => IsInRole(ROLE_CONTENT_WRITER);
        public bool IsWriterOrHigher => IsWriter || IsPublisherOrHigher;

        public bool IsCommentModerator => IsInRole(ROLE_COMMENT_MODERATOR);
        public bool IsCommentModeratorOrHigher => IsCommentModerator || IsPublisherOrHigher;
        public bool IsCommentAppellateJudge => IsInRole(ROLE_COMMENT_APPELLATE_JUDGE);
        public bool IsCommentAppellateJudgeOrHigher => IsCommentAppellateJudge || IsAdminOrHigher;

        public bool IsEventManager => IsInRole(ROLE_EVENT_MANAGER);
        public bool IsEventManagerOrHigher => IsAdminOrHigher || IsEventManager;
        public bool IsEventTicketManager => IsInRole(ROLE_EVENT_TICKET_MANAGER);
        public bool IsEventTicketManagerOrHigher => IsEventManagerOrHigher || IsEventTicketManager;

        public bool IsMemberManager => IsInRole(ROLE_MEMBER_MANAGER);
        public bool IsMemberManagerOrHigher => IsAdminOrHigher || IsMemberManager;
        public bool IsSubscriptionManager => IsInRole(ROLE_SUBSCRIPTION_MANAGER);
        public bool IsSubscriptionManagerOrHigher => IsAdminOrHigher || IsSubscriptionManager;

        public bool CanPublish => IsPublisherOrHigher;
        public bool CanCreateContent => IsWriterOrHigher;
        public bool CanManageEvents => IsEventManagerOrHigher;
        public bool CanManageEventTickets => IsEventTicketManagerOrHigher;
        public bool CanManageMembers => IsMemberManagerOrHigher;
        public bool CanManageSubscriptions => IsSubscriptionManagerOrHigher;

        public bool IsInRole(string role) => Roles.Contains(role);

        public bool CanChangeRolesOfOtherUser(RoleAbilities other)
        {
            if (IsOwner)
                return true;

            if (IsAdmin)
            {
                if (other.IsOwner)
                    return false;

                return true;
            }

            return false;
        }

        public bool CanManageOtherUser(RoleAbilities other)
        {
            if (IsOwner)
                return true;

            if (IsAdmin)
            {
                if (other.IsOwner)
                    return false;

                return true;
            }

            if (IsMemberManager)
            {
                var highest = other.GetHighestPrivilegedRole();
                if (highest != null)
                    return false;

                return true;
            }

            return false;
        }

        public string? GetHighestPrivilegedRole()
        {
            if (IsOwner)
                return ROLE_OWNER;
            if (IsAdmin)
                return ROLE_ADMIN;
            if (IsBackup)
                return ROLE_BACKUP;
            if (IsBot)
                return ROLE_BOT_VERIFICATION;

            return null;
        }
    }
}
