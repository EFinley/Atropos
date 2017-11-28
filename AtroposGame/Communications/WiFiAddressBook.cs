﻿using System;
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
    public static class AddressBook
    {
        public static List<string> Names = new List<string>();
        public static List<string> IPaddresses = new List<string>();
        public static List<Role[]> Roles = new List<Role[]>();
        public static List<MessageTarget> Targets = new List<MessageTarget>();

        private static int WhoHasItListedFirst(Role role)
        {
            var peakNumberOfRoles = Roles.Max(m => m.Length);
            for (int listingIndex = 0; listingIndex < peakNumberOfRoles; listingIndex++)
            {
                for (int memberIndex = 0; memberIndex < Names.Count; memberIndex++)
                {
                    if (Roles[memberIndex].ElementAtOrDefault(listingIndex) == role) return memberIndex;
                }
            }
            return -1;
        }

        public static void Add(MessageTarget target, string IPaddress)
        {
            var i = IPaddresses.IndexOf(IPaddress);
            if (i == -1) i = Names.IndexOf(target.Name);
            if (i != -1)
            {
                Targets[i] = target;
                if (target is TeamMember) Roles[i] = (target as TeamMember).Roles.ToArray();
                else if (Roles[i] == null || Roles[i].Length == 0) Roles[i] = new Role[] { Role.Any };
                IPaddresses[i] = IPaddress;
            }
            else
            {
                Names.Add(target.Name);
                if (target is TeamMember) Roles.Add( (target as TeamMember).Roles.ToArray());
                else Roles.Add(new Role[] { Role.Any });
                IPaddresses.Add(IPaddress);
            }

            // Also, add it to the master list under Runners.
            Runners.All.Members.Add((SenderAndReceiver)target);
        }

        public static MessageTarget Resolve(string identifier)
        {
            int i;
            i = IPaddresses.IndexOf(identifier);
            if (i == -1) i = Names.IndexOf(identifier);
            if (i == -1 && Enum.TryParse<Role>(identifier, out Role role))
                i = WhoHasItListedFirst(role);
            if (i == -1)
            {
                for (int j = 0; j < Targets.Count; j++)
                {
                    var tmAKA = Targets.ElementAtOrDefault(j) as TeamMemberAKA;
                    if (tmAKA?.ReferredToAs == identifier) return tmAKA;
                }
            }
            var result = (i >= 0) ? Targets[i] : TeamMember.Nobody;
            if ((result as TeamMember)?.Name == identifier) return result;
            else return new TeamMemberAKA((result as TeamMember), identifier);
        }
    }
}