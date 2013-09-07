using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;

namespace Messaging
{
    [Serializable]
    public struct BusEndpoint : IEquatable<BusEndpoint>
    {
        public readonly string MachineName;

        public readonly string QueueName;

        public BusEndpoint(string machineName, string queueName)
        {
            MachineName = IsLocalhost(machineName) ? Environment.MachineName : machineName;
            QueueName = queueName;
        }

        private static bool IsLocalhost(string machineName) { return String.IsNullOrWhiteSpace(machineName) || machineName.Equals(".", StringComparison.InvariantCultureIgnoreCase) || machineName.Equals("localhost", StringComparison.InvariantCultureIgnoreCase); }

        public override bool Equals(object obj)
        {
            if (Object.ReferenceEquals(obj, null))
                return false;
            if (Object.ReferenceEquals(this, obj))
                return true;
            
            return obj is BusEndpoint &&
                   this.Equals((BusEndpoint)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return MachineName.GetHashCode() * 31 + QueueName.GetHashCode(); }
        }

        public bool Equals(BusEndpoint other)
        {
            return this.MachineName == other.MachineName &&
                   this.QueueName == other.QueueName;
        }

        public static bool operator ==(BusEndpoint left, BusEndpoint right) { return Equals(left, right); }

        public static bool operator !=(BusEndpoint left, BusEndpoint right) { return !Equals(left, right); }
    }
}