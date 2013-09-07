using System;

namespace Messaging
{
    [Serializable]
    public struct BusEndpoint : IEquatable<BusEndpoint>
    {
        public readonly string MachineName;

        public readonly string QueueName;

        public BusEndpoint(string machineName, string queueName)
        {
            if(String.IsNullOrWhiteSpace(queueName)) throw new ArgumentException("queueName");

            MachineName = IsLocalhost(machineName) ? Environment.MachineName : machineName;
            QueueName = queueName;
        }

        private static bool IsLocalhost(string machineName)
        {
            return String.IsNullOrWhiteSpace(machineName) ||
                   machineName.Equals(".", StringComparison.InvariantCultureIgnoreCase) ||
                   machineName.Equals("localhost", StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;
            
            return obj is BusEndpoint &&
                   Equals((BusEndpoint)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return MachineName.GetHashCode() * 31 + QueueName.GetHashCode(); }
        }

        public bool Equals(BusEndpoint other)
        {
            return MachineName == other.MachineName &&
                   QueueName == other.QueueName;
        }

        public static bool operator ==(BusEndpoint left, BusEndpoint right) { return Equals(left, right); }

        public static bool operator !=(BusEndpoint left, BusEndpoint right) { return !Equals(left, right); }
    }
}