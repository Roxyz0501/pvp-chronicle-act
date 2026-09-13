using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace PvPChronicle
{
    internal sealed class AllyResolver
    {
        private readonly object repository;
        private readonly MethodInfo getPlayerId, getCombatants;
        public AllyResolver(object repository)
        {
            this.repository = repository;
            var type = repository.GetType().GetInterfaces().FirstOrDefault(x => x.FullName == "FFXIV_ACT_Plugin.Common.IDataRepository") ?? repository.GetType();
            getPlayerId = type.GetMethod("GetCurrentPlayerID");
            getCombatants = type.GetMethod("GetCombatantList");
        }
        public bool IsAlly(uint playerId, uint killerId)
        {
            try
            {
                if (getPlayerId == null || getCombatants == null || Convert.ToUInt32(getPlayerId.Invoke(repository, null)) != playerId) return false;
                var combatants = getCombatants.Invoke(repository, null) as IEnumerable;
                if (combatants == null) return false;
                foreach (var combatant in combatants)
                {
                    if (combatant == null) continue;
                    var type = combatant.GetType();
                    var id = type.GetProperty("ID"); var party = type.GetProperty("PartyType");
                    if (id == null || party == null || Convert.ToUInt32(id.GetValue(combatant, null)) != killerId) continue;
                    var membership = party.GetValue(combatant, null);
                    // FFXIV_ACT_Plugin: None=0, Party=1, Alliance=2. Unknown membership is never accepted.
                    return membership != null && (Convert.ToInt32(membership) == 1 || Convert.ToInt32(membership) == 2);
                }
            }
            catch { }
            return false;
        }
    }
}
