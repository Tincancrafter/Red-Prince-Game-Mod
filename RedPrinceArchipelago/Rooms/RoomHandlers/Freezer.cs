using RedPrinceArchipelago.Items;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using RedPrinceArchipelago.Utils;
using UnityEngine;

namespace RedPrinceArchipelago.Rooms.RoomHandlers;

class Freezer : RoomHandler
{
    public override void OnRoomDrafted(GameObject roomGameObject)
    {
        PlayMakerFSM ItemDropFSM = roomGameObject.transform.Find("_GAMEPLAY/ICE DOORS/Door 20/6")?.GetComponent<PlayMakerFSM>();
        if (ItemDropFSM != null)
        {
            bool found = !ModItemManager.UpgradeDisks.FoundLocations.Contains("FREEZER");
            Logging.LogWarning(found);
            FsmBool CanSpawnDisk = ItemDropFSM.AddBoolVariable("CanSpawnDisk");
            CanSpawnDisk.Value = found;
            ItemDropFSM.GetState("State 1").GetFirstActionOfType<BoolTest>().boolVariable = CanSpawnDisk;
        }
        else
        {
            Logging.LogWarning("Error changing Freezer Upgrade disk spawn logic.");
        }
    }
}

