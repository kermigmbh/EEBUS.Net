using EEBUS.Net.EEBUS.Models;
using EEBUS.Net.EEBUS.Models.Data;

namespace EEBUS.Models
{
    public delegate void StateChangedHandler(Connection.EState state, RemoteDevice device);
    public delegate void RemoteDeviceFoundHandler(RemoteDevice device);

    public class Devices
    {
        public Devices()
        {
        }

        public LocalDevice Local { get; private set; }
        public List<RemoteDevice> Remote { get; set; } = [];
        public List<PairedDevice> Paired { get; set; } = [];

        private object mutex = new();


        public event RemoteDeviceFoundHandler RemoteDeviceFound;
        public event StateChangedHandler ServerStateChanged;
        public event StateChangedHandler ClientStateChanged;

        protected virtual void FireRemoteDeviceFound(RemoteDevice device)
        {
            RemoteDeviceFound?.Invoke(device);
        }

        public LocalDevice GetOrCreateLocal(byte[] ski, DeviceSettings settings)
        {
            lock (this.mutex)
            {
                if (null == Local)
                    Local = new LocalDevice(ski, settings);

                return Local;
            }
        }

        public RemoteDevice GetOrCreateRemote(string id, string ski, string url, string name)
        {
            if (null != Local && Local.SKI == new SKI(ski))
                return null;

            RemoteDevice? remote = null;
            bool foundNew = false;

            lock (this.mutex)
            {
                remote = Remote.FirstOrDefault(r => r.Id == id);

                if (null == remote)
                {
                    remote = new RemoteDevice(id, ski, url, name, FireServerStateChanged, FireClientStateChanged);
                    Remote.Add(remote);
                    foundNew = true;
                }
            }

            if (foundNew)
                FireRemoteDeviceFound(remote);
            remote.ReNewAge();
            return remote;
        }

        public PairedDevice GetOrCreatePaired(string trustPar, string trustId, ShipTrustType trustType = ShipTrustType.AddCu)
        {
            PairedDevice? paired = null;
            lock (this.mutex)
            {
                paired = Paired.FirstOrDefault(p => p.TrustId == trustId && p.TrustPar == trustPar);

                if (paired == null)
                {
                    paired = new PairedDevice(trustPar, trustId, trustType);
                    if (trustType == ShipTrustType.AddCu)   //We can only ever have one device at a time paired with addCu (according to spec), so we set all other ones to none and add the new one
                    {
                        IEnumerable<PairedDevice> pairedAddCu = Paired.Where(p => p.TrustType == ShipTrustType.AddCu);
                        foreach (var item in pairedAddCu)
                        {
                            item.TrustType = ShipTrustType.None; 
                        }
                    }
                    Paired.Add(paired);
                }
            }
            return paired;
        }

        public RemoteDevice? GetRemote(string id)
        {
            lock (this.mutex)
            {
                return Remote.FirstOrDefault(r => r.Id == id);
            }
        }

        public void FireServerStateChanged(Connection.EState state, RemoteDevice device)
        {
            ServerStateChanged?.Invoke(state, device);
        }

        public void FireClientStateChanged(Connection.EState state, RemoteDevice device)
        {
            ClientStateChanged?.Invoke(state, device);
        }

        public void GarbageCollect()
        {
            lock (this.mutex)
            {
                foreach (RemoteDevice remote in Remote.ToArray())
                {
                    if (remote.OlderThan(new TimeSpan(1, 0, 0)))
                        Remote.Remove(remote);
                }
            }
        }
    }
}
