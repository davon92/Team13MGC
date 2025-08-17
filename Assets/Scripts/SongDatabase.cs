using UnityEngine;

[CreateAssetMenu(menuName = "Songs/Database")]
public class SongDatabase : ScriptableObject
{
    public SongInfo[] songs;

    public int Count => songs != null ? songs.Length : 0;
    public SongInfo this[int i] => songs[(i % Count + Count) % Count];
}