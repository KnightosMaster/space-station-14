﻿using Content.Server.GameObjects.Components.Power;
using Content.Server.GameObjects.Components.Power.ApcNetComponents;
using Robust.Shared.ViewVariables;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Content.Server.GameObjects.Components.NodeContainer.NodeGroups
{
    public interface IApcNet
    {
        void AddApc(ApcComponent apc);

        void RemoveApc(ApcComponent apc);

        void AddPowerProvider(PowerProviderComponent provider);

        void RemovePowerProvider(PowerProviderComponent provider);

        void UpdatePowerProviderReceivers(PowerProviderComponent provider);

        void Update(float frameTime);
    }

    [NodeGroup(NodeGroupID.Apc)]
    public class ApcNetNodeGroup : BaseNetConnectorNodeGroup<BaseApcNetComponent, IApcNet>, IApcNet
    {
        [ViewVariables]
        private readonly Dictionary<ApcComponent, BatteryComponent> _apcBatteries = new Dictionary<ApcComponent, BatteryComponent>();

        [ViewVariables]
        private readonly Dictionary<PowerProviderComponent, List<PowerReceiverComponent>> _providerReceivers = new Dictionary<PowerProviderComponent, List<PowerReceiverComponent>>();

        //Debug property
        [ViewVariables]
        private int TotalReceivers => _providerReceivers.SelectMany(kvp => kvp.Value).Count();

        private IEnumerable<BatteryComponent> AvailableBatteries => _apcBatteries.Where(kvp => kvp.Key.MainBreakerEnabled).Select(kvp => kvp.Value);

        public static readonly IApcNet NullNet = new NullApcNet();

        #region IApcNet Methods

        protected override void SetNetConnectorNet(BaseApcNetComponent netConnectorComponent)
        {
            netConnectorComponent.Net = this;
        }

        public void AddApc(ApcComponent apc)
        {
            _apcBatteries.Add(apc, apc.Battery);
        }

        public void RemoveApc(ApcComponent apc)
        {
            _apcBatteries.Remove(apc);
            if (!_apcBatteries.Any())
            {
                foreach (var receiver in _providerReceivers.SelectMany(kvp => kvp.Value))
                {
                    receiver.HasApcPower = false;
                }
            }
        }

        public void AddPowerProvider(PowerProviderComponent provider)
        {
            _providerReceivers.Add(provider, provider.LinkedReceivers.ToList());
        }

        public void RemovePowerProvider(PowerProviderComponent provider)
        {
            _providerReceivers.Remove(provider);
        }

        public void UpdatePowerProviderReceivers(PowerProviderComponent provider)
        {
            Debug.Assert(_providerReceivers.ContainsKey(provider));
            _providerReceivers[provider] = provider.LinkedReceivers.ToList();
        }

        public void Update(float frameTime)
        {
            var totalCharge = 0.0;
            var totalMaxCharge = 0;
            foreach (var battery in AvailableBatteries)
            {
                totalCharge += battery.CurrentCharge;
                totalMaxCharge += battery.MaxCharge;
            }
            var availablePowerFraction = totalCharge / totalMaxCharge;
            foreach (var receiver in _providerReceivers.SelectMany(kvp => kvp.Value))
            {
                receiver.HasApcPower = TryUsePower(receiver.Load * frameTime);
            }
        }

        private bool TryUsePower(float neededCharge)
        {
            foreach (var battery in AvailableBatteries)
            {
                if (battery.TryUseCharge(neededCharge)) //simplification - all power needed must come from one battery
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        private class NullApcNet : IApcNet
        {
            public void AddApc(ApcComponent apc) { }
            public void AddPowerProvider(PowerProviderComponent provider) { }
            public void RemoveApc(ApcComponent apc) { }
            public void RemovePowerProvider(PowerProviderComponent provider) { }
            public void UpdatePowerProviderReceivers(PowerProviderComponent provider) { }
            public void Update(float frameTime) { }
        }
    }
}
