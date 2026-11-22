using System.Collections.Generic;
namespace MyGame
{
    /// <summary>Capture the actual locked-to-open transition; replay clears must not announce old unlocks.</summary>
    public static class StageUnlocks
    {
        public static HashSet<string> CaptureLocked(StageDatabase database,SaveData save)
        {
            var result=new HashSet<string>();
            foreach(var node in database.Nodes)
                if(node!=null&&NodeStateResolver.Resolve(node,save)==NodeState.Locked)result.Add(node.NodeId);
            return result;
        }
        public static string[] NewlyOpened(StageDatabase database,SaveData save,HashSet<string> lockedBefore)
        {
            var result=new List<string>();
            if(lockedBefore==null)return result.ToArray();
            foreach(var node in database.Nodes)
                if(node!=null&&lockedBefore.Contains(node.NodeId)&&NodeStateResolver.Resolve(node,save)==NodeState.Open)result.Add(node.NodeId);
            return result.ToArray();
        }
        public static string DisplayNumber(string id)
        {
            if(string.IsNullOrEmpty(id))return "";
            var parts=id.Split('_');
            if(parts.Length==2&&parts[0].StartsWith("W"))return parts[0].Substring(1)+"-"+(parts[1].StartsWith("S")?parts[1].Substring(1):parts[1]);
            return id;
        }
    }
}
