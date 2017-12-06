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

using static Atropos.Communications.WifiBaseClass;

namespace Atropos.Communications
{
    public enum Role
    {
        None,
        Hitter,
        Hacker,
        Sorceror,
        Spy,
        Any,
        All,
        Self,
        NPC,
        GM
    }

    public class TeamMember : SenderAndReceiver
    {
        public virtual List<Role> Roles { get; set; }
        public Role Role // Always refers to Roles[0], even if Roles doesn't exist yet.
        {
            get { return Roles?.ElementAtOrDefault(0) ?? Role.None; }
            set
            {
                if (Roles == null || Roles.Count == 0) Roles = new List<Role> { value };
                else Roles[0] = value;
            }
        }

        public TeamMember() : base() { }

        public static TeamMember Nobody = new TeamMember() { Name = "Nobody", Role = Role.None };

        public static implicit operator string(TeamMember teamMember) => teamMember.Name;
        public string IntroductionString() { return IntroductionAs(this); }

        #region Static "conversion operators" for a TeamMember to and from an "Introduction string"
        public static string IntroductionAs(TeamMember member)
        {
            return IntroductionAs(member.Name, member.IPaddress, member.Roles.ToArray());
        }
        public static string IntroductionAs(string asWhatName, string IPaddress, params Role[] asWhatRoles)
        {
            var result = $"{INTRODUCING}{NEXT}{asWhatName}{NEXT}{IPaddress}";
            foreach (var asWhatRole in asWhatRoles)
            {
                if (asWhatRole == Role.Self) continue; // Don't send that!!
                result += $"{NEXT}{asWhatRole.ToString()}";
            }
            return result;
        }
        public static TeamMember FromIntroductionString(string introString)
        {
            var substrings = introString.Split(onNEXT);

            var respName = substrings[1];
            var respIP = substrings[2];
            var roles = substrings.Skip(3).Select(r => (Role)Enum.Parse(typeof(Role), r)).ToList(); // Turns the string "Hitter" into Role.Hitter, etc.
            //Log.Debug(_tag, $"Received {POLL_RESPONSE} from {sender}, giving their name ({respName}) and role ({respRole}).  Adding to address book.");

            return new TeamMember()
            {
                Name = respName,
                IPaddress = respIP,
                Roles = roles
            };
        }
        #endregion
    }

    public class TeamMemberAKA : TeamMember
    {
        private TeamMember _member;
        public string ReferredToAs { get; set; }
        public string TrueName { get => _member.Name; }
        public override string Name { get => ReferredToAs; }

        public override List<Role> Roles { get => _member.Roles; set => _member.Roles = value; }

        public TeamMemberAKA(TeamMember member, string referredToAs) : base()
        {
            _member = member;
            ReferredToAs = referredToAs;
        }
    }


    // Represents any grouping of characters (or of smaller teams)
    public class Team : SenderAndReceiver
    {
        public override string Name { get; set; } = "Group";
        public List<SenderAndReceiver> Members;
        public List<TeamMember> TeamMembers
        {
            get
            {
                var result = new List<TeamMember>();
                foreach (var m in Members)
                {
                    if (m is TeamMember member && m != TeamMember.Nobody) result.Add(member);
                    else if (m is Team team) result.AddRange(team.TeamMembers);
                }
                return result;
            }
        }
        public Team(IEnumerable<SenderAndReceiver> source) : base()
        {
            Members = source?.ToList() ?? new List<SenderAndReceiver>();
        }
        public Team(params SenderAndReceiver[] members) : this(source: members) { }

        public int NumberOfAddressees { get { return Members.Count; } }
        public int NumberOfAddresseesTotal { get { return Members.Select(m => (m as Team)?.NumberOfAddresseesTotal ?? 1).Sum(); } }
    }


    public static class Runners
    { 
        public static Team All { get; set; } = new Team();
        public static Team Team { get { return new Team(All.TeamMembers.Where(m => !m.Roles.Contains(Role.NPC) && !m.Roles.Contains(Role.GM))); } }
        public static Team NPCs { get { return new Team(All.TeamMembers.Where(m => m.Roles.Contains(Role.NPC))); } }
        public static Team GMs { get { return new Team(All.TeamMembers.Where(m => m.Roles.Contains(Role.GM))); } }
        public static Team None { get; } = new Team();

        // Utility method for the ensuing - so if you have a Hitter/Hacker, and a pure Hacker, Team.Hacker gets you the latter.
        private static TeamMember WhoHasItListedFirst(Role role)
        {
            var peakNumberOfRoles = Team.TeamMembers.Max(m => m.Roles.Count);
            for (int i = 0; i < peakNumberOfRoles; i++)
            {
                foreach (var m in Team.TeamMembers)
                {
                    if (m.Roles.ElementAtOrDefault(i) == role) return new TeamMemberAKA(m, role.ToString());
                }
            }
            return TeamMember.Nobody;
        }

        public static TeamMember Hitter { get { return WhoHasItListedFirst(Role.Hitter); } }
        public static TeamMember Hacker { get { return WhoHasItListedFirst(Role.Hacker); } }
        public static TeamMember Sorceror { get { return WhoHasItListedFirst(Role.Sorceror); } }
        public static TeamMember Spy { get { return WhoHasItListedFirst(Role.Spy); } }

        public static Team Hitters { get { return new Team(Team.TeamMembers.Where(t => t.Roles.Contains(Role.Hitter))) { Name = "Hitters" }; } }
        public static Team Hackers { get { return new Team(Team.TeamMembers.Where(t => t.Roles.Contains(Role.Hacker))) { Name = "Hackers" }; } }
        public static Team Sorcerors { get { return new Team(Team.TeamMembers.Where(t => t.Roles.Contains(Role.Sorceror))) { Name = "Sorcerors" }; } }
        public static Team Spies { get { return new Team(Team.TeamMembers.Where(t => t.Roles.Contains(Role.Spy))) { Name = "Spies" }; } }

        public static Team ByNames(params string[] names)
        {
            return new Team(All.TeamMembers.Where(t => names.Contains(t.Name)));
        }
        public static Team ByRoles(params Role[] roles)
        {
            return new Team(All.TeamMembers.Where(t => roles.Intersect(t.Roles).Count() > 0));
        }
    }
}