using FortisAPI.Standard.Models;
using IT.WebServices.Authorization.Payment.Fortis.Clients;
using IT.WebServices.Authorization.Payment.Fortis.Models;
using IT.WebServices.Helpers;

namespace IT.WebServices.Authorization.Payment.Fortis.Helpers
{
    public class FortisContactHelper
    {
        private readonly FortisClient client;
        private readonly SettingsHelper settingsHelper;

        public FortisContactHelper(FortisClient client, SettingsHelper settingsHelper)
        {
            this.client = client;
            this.settingsHelper = settingsHelper;
        }

        public async Task<ResponseContact?> Create(UserModel user, CancellationToken cancellationToken = default)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                var contact = await GetByAccountNumber(user);
                if (contact != null)
                    return contact;

                var req = new V1ContactsRequest()
                {
                    ContactApiId = "u" + user.Id.ToString().Replace("-", ""),
                    LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID,
                };

                //if (!string.IsNullOrWhiteSpace(user.Email))
                //    req.Email = user.Email;
                if (!string.IsNullOrWhiteSpace(user.FirstName))
                    req.FirstName = user.FirstName;

                if (!string.IsNullOrWhiteSpace(user.LastName))
                    req.LastName = user.LastName;
                else
                    req.LastName = "None";

                cancellationToken.ThrowIfCancellationRequested();

                return client.Client.ContactsController.CreateANewContact(req, new());
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async Task<ResponseContact?> Get(string contactId)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                return await client.Client.ContactsController.ViewSingleContactAsync(contactId);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }

        public async Task<Dictionary<string, List1>?> GetAll(int triesLeft = 5)
        {
            if (!client.IsEnabled) return [];
            if (client.Client == null) return [];

            int errors = 0;
            int page = 1;
            int size = 1000;

            var ret = new List<List1>();

            try
            {
                while (errors < 10)
                {
                    var list = await client.Client.ContactsController.ListAllContactsAsync(
                        new Page() { Number = page, Size = size },
                        null,
                        new Filter1() { LocationId = settingsHelper.Owner.Subscription.Fortis.LocationID },
                        new List<ExpandEnum>());

                    if (list?.List == null)
                    {
                        errors++;
                        continue;
                    }

                    if (ret.Any(i => i.Id == list.List.FirstOrDefault()?.Id))
                        break;

                    ret.AddRange(list.List);

                    page++;

                    Console.WriteLine($"Loading Contacts: {ret.Count}");

                    if (list.List.Count < size)
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);

                if (triesLeft > 0)
                    return await GetAll(triesLeft - 1);
                else
                    return null;
            }

            if (ret.Count % size == 0)
                throw new Exception($"{ret.Count} is divisible by {size} this normally indicates an error. Aborting!");

            return ret.ToDictionary(i => i.Id);
        }

        public async Task<ResponseContact?> GetByAccountNumber(UserModel user)
        {
            return await GetByAccountNumber(user.Id);
        }

        public async Task<ResponseContact?> GetByAccountNumber(Guid userId)
        {
            if (!client.IsEnabled) return null;
            if (client.Client == null) return null;

            try
            {
                var list = await client.Client.ContactsController.ListAllContactsAsync(new Page() { Number = 1, Size = 1 }, null, new Filter1()
                {
                    ContactApiId = "u" + userId.ToString().Replace("-", "")
                }, new());

                var item = list?.List?.FirstOrDefault();
                if (item != null)
                    return item.ToResponseContact();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

            return null;
        }
    }
}
