using RedPrinceArchipelago.Items;
using RedPrinceArchipelago.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace RedPrinceArchipelago.Rooms.RoomHandlers
{
    public class DraftingStudio : RoomHandler {

        public DraftingStudio()
        {
            Logging.Log("Initializing Drafting Studio.");
        }

        public override void OnRoomDrafted(GameObject roomGameObject)
        {
            ModInstance.GlobalManager.GetBoolVariable("Clock Tower Unlocked").Value = Plugin.ModRoomManager.GetRoomByName("CLOCK TOWER").IsUnlocked;
            ModInstance.GlobalManager.GetBoolVariable("The Kennel Unlocked").Value = Plugin.ModRoomManager.GetRoomByName("THE KENNEL").IsUnlocked;
            ModInstance.GlobalManager.GetBoolVariable("Vestibule Unlocked").Value = Plugin.ModRoomManager.GetRoomByName("VESTIBULE").IsUnlocked;
            ModInstance.GlobalManager.GetBoolVariable("Dovecote Unlocked").Value = Plugin.ModRoomManager.GetRoomByName("DOVECOTE").IsUnlocked;
            ModInstance.GlobalManager.GetBoolVariable("Solarium Unlocked").Value = Plugin.ModRoomManager.GetRoomByName("SOLARIUM").IsUnlocked;
            ModInstance.GlobalManager.GetBoolVariable("Dormitory Unlocked").Value = Plugin.ModRoomManager.GetRoomByName("DORMITORY").IsUnlocked;
            ModInstance.GlobalManager.GetBoolVariable("Casino Unlocked").Value = Plugin.ModRoomManager.GetRoomByName("CASINO").IsUnlocked;

        }
    }
}
