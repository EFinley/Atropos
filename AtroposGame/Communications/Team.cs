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

namespace com.Atropos.Communications
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
        public Role Role
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

        //public interface IHaveAnAlias { string Alias { get; } }

        public static implicit operator string(TeamMember teamMember) => teamMember.Name;
    }

    public class TeamMemberAKA : TeamMember   //, TeamMember.IHaveAnAlias
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
                    var member = m as TeamMember;
                    if (member != null) result.Add(member);
                    else if (m is Team) result.AddRange((m as Team).TeamMembers);
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