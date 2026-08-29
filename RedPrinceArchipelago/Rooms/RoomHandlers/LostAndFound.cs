using RedPrinceArchipelago.Items;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using RedPrinceArchipelago.Utils;
using UnityEngine;

namespace RedPrinceArchipelago.Rooms.RoomHandlers;

class LostAndFound : RoomHandler
{
    public LostAndFound()
    {
        AllowanceTokens.Add("Lost & Found");
    }
    public override void OnAllowanceTokenCollected(string token)
    {
        ModInstance.ModEventHandler.OnMoraJaiSolved("Lost & Found");
    }
    public override void OnRoomDrafted(GameObject roomGameObject)
    {
        RoomGameObject = roomGameObject;
        PlayMakerFSM ItemDropFSM = roomGameObject.transform.Find("_GAMEPLAY/9")?.gameObject?.GetComponent<PlayMakerFSM>();
        if (ItemDropFSM != null)
        {
            bool found = !ModItemManager.UpgradeDisks.FoundLocations.Contains("LOST AND FOUND");
            Logging.LogWarning(found);
            FsmBool CanSpawnDisk = ItemDropFSM.AddBoolVariable("CanSpawnDisk");
            CanSpawnDisk.Value = found;
            ItemDropFSM.GetState("State 4").GetFirstActionOfType<BoolTest>().boolVariable = CanSpawnDisk;
        }
        else {
            Logging.LogWarning("Error changing Lost and Found Upgrade disk spawn logic.");
        }
    }

    /// <summary>
    /// Removes the collected disk object. Lost &amp; Found does not run the usual
    /// generic disk-pickup cleanup, leaving the disk in its rare-item slot.
    /// </summary>
    public void RemoveCollectedUpgradeDisk()
    {
        GameObject diskObject = RoomGameObject?.transform.Find("_GAMEPLAY/9")?.gameObject;
        if (diskObject == null)
        {
            Logging.LogWarning("Unable to remove the collected Lost & Found upgrade disk.", "UpgradeDisks");
            return;
        }

        GameObject.Destroy(diskObject);
        Logging.Log("Removed the collected Lost & Found upgrade disk.", "UpgradeDisks");
    }
}
