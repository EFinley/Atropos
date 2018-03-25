using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Net.Sockets;
using Android.Util;
using System.Threading.Tasks;
using MiscUtil;
using Nito.AsyncEx;
using System.Threading;
using System.Reflection;
using Role = Atropos.Characters.Role;
using Atropos.Communications.Bluetooth;

namespace Atropos.Communications
{
    

    public class CommsContact : MessageTarget
    {
        public BTPeer BtPeer { get; set; }
        public override BluetoothClient Client { get; set; }

        private string _status; // Encapsulates a lot of possible stuff - connection status, char status, etc. - basically a summary field.
        public string Status
        {
            get
            {
                if (String.IsNullOrEmpty(_status)) return BtPeer?.Device.BondState.ToString();
                else return _status;
            }
        }

        public virtual List<Role> Roles { get; set; }
        public Role Role
        {
            get { return Roles?.ElementAtOrDefault(0) ?? Role.None; }
            set
            {
                if (Roles == null || Roles.Count == 0) Roles = new List<Role> { value };
                else Roles[0] = value;
            }
        }

        //public virtual Dictionary<string, Characteristic> Characteristics { get; set; } = new Dictionary<string, Characteristic>();

        //public CommsContact() : base()
        //{
        //    foreach (var kvp in Characteristic.GUIDs)
        //    {
        //        var newCharacteristic = new Characteristic<string>(this, kvp.Key);
        //        newCharacteristic.GUID = kvp.Value;
        //    }
        //}

        public static CommsContact Me = new CommsContact() { Name = "Me", Role = Role.Self, Client = new BluetoothClient.SelfClientClass() };
        public static CommsContact Nobody = new CommsContact() { Name = "Nobody", Role = Role.None };

        //public interface IHaveAnAlias { string Alias { get; } }

        public static implicit operator string(CommsContact teamMember) => teamMember.Name;
    }

    public class TeamMemberAKA : CommsContact   //, CommsContact.IHaveAnAlias
    {
        private CommsContact _member;
        public string ReferredToAs { get; set; }
        public string TrueName { get => _member.Name; }
        public override string Name { get => ReferredToAs; }

        public override List<Role> Roles { get => _member.Roles; set => _member.Roles = value; }

        public TeamMemberAKA(CommsContact member, string referredToAs) : base()
        {
            _member = member;
            ReferredToAs = referredToAs;
        }
    }


    // Represents any grouping of characters (or of smaller teams)
    public class CommsGroup : MessageTarget
    {
        public override string Name { get; set; } = "Group";
        private BluetoothClient.GroupClientClass _client;
        public override BluetoothClient Client { get => _client; set => _client = value as BluetoothClient.GroupClientClass; }
        public List<MessageTarget> Members;
        public HashSet<CommsContact> Contacts
        {
            get
            {
                var result = new HashSet<CommsContact>();
                foreach (var m in Members)
                {
                    if (m is CommsContact member) result.Add(member);
                    else if (m is CommsGroup group) result.UnionWith(group.Contacts);
                }
                return result;
            }
        }
        public CommsGroup(IEnumerable<MessageTarget> source) : base()
        {
            Members = source?.ToList() ?? new List<MessageTarget>();
            _client = new BluetoothClient.GroupClientClass(this);
        }
        public CommsGroup(params MessageTarget[] members) : this(source: members) { }

        public int NumberOfAddressees { get { return Members.Count; } }
        public int NumberOfAddresseesTotal { get { return Members.Select(m => (m as CommsGroup)?.NumberOfAddresseesTotal ?? 1).Sum(); } }
    }


    public static class HeyYou
    { 
        public static CommsGroup Everybody { get; set; } = new CommsGroup();
        public static CommsGroup Runners { get { return new CommsGroup(Everybody.Contacts.Where(m => !m.Roles.Contains(Role.NPC) && !m.Roles.Contains(Role.GM))); } }
        public static CommsGroup MyTeammates { get { return new CommsGroup(Runners.Contacts.Where(m => !m.Roles.Contains(Role.Self))); } }
        public static CommsGroup NPCs { get { return new CommsGroup(Everybody.Contacts.Where(m => m.Roles.Contains(Role.NPC))); } }
        public static CommsGroup GMs { get { return new CommsGroup(Everybody.Contacts.Where(m => m.Roles.Contains(Role.GM))); } }
        public static CommsGroup None { get; } = new CommsGroup();

        private static CommsContact WhoHasItListedFirst(Role role)
        {
            var peakNumberOfRoles = Runners.Contacts.Max(m => m.Roles.Count);
            for (int i = 0; i < peakNumberOfRoles; i++)
            {
                foreach (var m in Runners.Contacts)
                {
                    if (m.Roles.ElementAtOrDefault(i) == role) return new TeamMemberAKA(m, role.ToString());
                }
            }
            return CommsContact.Nobody;
        }

        public static CommsContact Hitter { get { return WhoHasItListedFirst(Role.Hitter); } }
        public static CommsContact Hacker { get { return WhoHasItListedFirst(Role.Hacker); } }
        public static CommsContact Sorceror { get { return WhoHasItListedFirst(Role.Sorceror); } }
        public static CommsContact Spy { get { return WhoHasItListedFirst(Role.Spy); } }

        public static CommsGroup Hitters { get { return new CommsGroup(Runners.Contacts.Where(t => t.Roles.Contains(Role.Hitter))) { Name = "Hitters" }; } }
        public static CommsGroup Hackers { get { return new CommsGroup(Runners.Contacts.Where(t => t.Roles.Contains(Role.Hacker))) { Name = "Hackers" }; } }
        public static CommsGroup Sorcerors { get { return new CommsGroup(Runners.Contacts.Where(t => t.Roles.Contains(Role.Sorceror))) { Name = "Sorcerors" }; } }
        public static CommsGroup Spies { get { return new CommsGroup(Runners.Contacts.Where(t => t.Roles.Contains(Role.Spy))) { Name = "Spies" }; } }

        public static CommsGroup ByName(params string[] names)
        {
            return new CommsGroup(Everybody.Contacts.Where(t => names.Contains(t.Name)));
        }
        public static CommsGroup ByRoles(params Role[] roles)
        {
            return new CommsGroup(Everybody.Contacts.Where(t => roles.Intersect(t.Roles).Count() > 0));
        }
    }
}