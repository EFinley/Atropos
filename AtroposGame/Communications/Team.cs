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
        Hitter,
        Hacker,
        Sorceror,
        Spy,
        GameMaster,
        None,
        Any,
        All
    }

    //TODO - Populate MessageTarget.ReturnAddress with own ID!
    public class TeamMember : SenderAndReceiver
    {
        public string Name;
        public Role Role;
        public Socket Socket;
        
    }

    

    public class Team : SenderAndReceiver
    {
        public List<SenderAndReceiver> Members;
        public Team(IEnumerable<SenderAndReceiver> source = null)
        {
            Members = source?.ToList() ?? new List<SenderAndReceiver>();
        }
        public  int NumberOfAddressees { get { return Members.Count; } }
        public int NumberOfAddresseesTotal { get { return Members.Select(m => (m as Team)?.NumberOfAddresseesTotal ?? 1).Sum(); } }

        #region Static methods for quick access (of various kinds)
        public static Team All { get; set; } = new Team();
        public static Team None { get; } = new Team();

        public static Team Hitters { get { return new Team(All.Members.Where(t => (t as TeamMember)?.Role == Role.Hitter)); } }
        public static TeamMember Hitter { get { return Hitters.Members.FirstOrDefault() as TeamMember; } }
        public static Team Hackers { get { return new Team(All.Members.Where(t => (t as TeamMember)?.Role == Role.Hacker)); } }
        public static TeamMember Hacker { get { return Hackers.Members.FirstOrDefault() as TeamMember; } }
        public static Team Sorcerors { get { return new Team(All.Members.Where(t => (t as TeamMember)?.Role == Role.Sorceror)); } }
        public static TeamMember Sorceror { get { return Sorcerors.Members.FirstOrDefault() as TeamMember; } }
        public static Team Spies { get { return new Team(All.Members.Where(t => (t as TeamMember)?.Role == Role.Spy)); } }
        public static TeamMember Spy { get { return Spies.Members.FirstOrDefault() as TeamMember; } }

        public static Team To(params string[] names)
        {
            return new Team(All.Members.Where(t => names.Contains((t as TeamMember).Name)));
        }
        public static Team To(params Role[] roles)
        {
            return new Team(All.Members.Where(t => roles.Contains((t as TeamMember)?.Role ?? Role.None)));
        }
        #endregion

        #region Equality implementation... no longer needed, but free.
        public override bool Equals(object obj)
        {
            if (obj is Team) return (this as IEquatable<Team>).Equals(obj as Team);
            return Members.Equals(obj);
        }
        public override int GetHashCode()
        {
            return Members.GetHashCode();
        }

        public bool Equals(Team other)
        {
            return Members.Equals(other.Members);
        }

        public static bool operator==(Team first, Team second) { return first.Equals(second); }
        public static bool operator!=(Team first, Team second) { return !first.Equals(second); }
        #endregion
    }
}