using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomCorridors : MonoBehaviour
{
    public DungeonGenerator generator;
    public DungeonSettings cfg;
    private Room roomclass;
    private RoomCorridors corridors;
    private CellularAutomata ca;
    private Tilemap2D tm2d;
    private Globals global;


    // ===================================================
    // Generate Corridors

    // Iterates through all unconnected rooms.  Finds a short corridor between two closest super-rooms and
    // generates that corridor as a new room.  Connects both ends to the rooms, making a bigger super-room.
    public IEnumerator ConnectRoomsByCorridors(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("ConnectRoomsByCorridors"); local_tm = true; }
        try
        {
            List<int> unconnected_rooms = new(); // start with all, and then remove as rooms are merged
            Vector2Int center_i = Vector2Int.zero;
            Vector2Int close_i = Vector2Int.zero;
            Vector2Int close_j = Vector2Int.zero;
            int connection_room_i = -1, connection_room_j = -1;

            BottomBanner.Show($"Connecting {global.rooms.Count} rooms by corridors...");

            // initialize unconnected rooms to include all room indexes
            for (int room_no = 0; room_no < global.rooms.Count; room_no++)
                unconnected_rooms.Add(room_no);

            while (unconnected_rooms.Count > 1)
            {
                //Debug.Log("unconnected_rooms = " + ListOfIntToString(unconnected_rooms));
                //Debug.Log("Room "+ +" neighbor_rooms = " + ListOfIntToString(unconnected_rooms));
                for (int xx = 0; xx < global.rooms.Count; xx++)
                {
                    List<int> all_connected_to_xx = roomclass.get_union_of_connected_room_indexes(xx);
                    Debug.Log("NEIGHBORS of Room " + xx + "; neighbors  = " + ca.ListOfIntToString(global.rooms[xx].neighbors));
                    Debug.Log("NEIGHBORS of Room " + xx + "; all_connected_to_xx  = " + ca.ListOfIntToString(all_connected_to_xx));
                }

                // Find two closest rooms (i and j),
                // and a point in each close to the other (close_i, close_j)
                // (not guaranteed to be THE closest, but good enough for room connections)
                Vector2Int closestPair = FindTwoClosestRooms(unconnected_rooms);
                if (closestPair == Vector2Int.zero)
                {
                    Debug.Log("Problem? No pairs found but unconnected_rooms.Count = " + unconnected_rooms.Count);
                    break; // no pairs found, exit loop
                }
                int i = closestPair.x;
                int j = closestPair.y;
                List<Vector2Int> all_tiles_i = roomclass.get_union_of_connected_room_cells(i);
                List<Vector2Int> all_tiles_j = roomclass.get_union_of_connected_room_cells(j);

                // Closest points between rooom i and room j.
                center_i = GetCenterOfTiles(all_tiles_i);
                close_j = tm2d.GetClosestPointInTilesList(all_tiles_j, center_i);
                close_i = tm2d.GetClosestPointInTilesList(all_tiles_i, close_j);

                connection_room_i = -1;  // initial assumptions to be adjusted in for loop
                connection_room_j = -1;
                for (int rn = 0; rn < global.rooms.Count; rn++)
                {
                    if (global.rooms[rn].tiles.Contains(close_i))
                    {
                        connection_room_i = rn;
                    }
                    if (global.rooms[rn].tiles.Contains(close_j))
                    {
                        connection_room_j = rn;
                    }
                }
                if (connection_room_i == -1)
                {
                    Debug.Log("ERROR: No connection_room_i(" + i + ") found");
                    //yield return tm.YieldOrDelay(5f);
                }
                if (connection_room_j == -1)
                {
                    Debug.Log("ERROR: No connection_room_j(" + j + ") found");
                    //yield return tm.YieldOrDelay(5f);
                }
                // find height of each corridor endpoint, limiting search to specific room
                int height_i = roomclass.GetHeightOfLocationFromOneRoom(global.rooms[connection_room_i], close_i);
                int height_j = roomclass.GetHeightOfLocationFromOneRoom(global.rooms[connection_room_j], close_j);

                // Carve the corridor and create a new room of it
                Room corridorRoom = generator.DrawCorridorSloped(close_i, close_j, height_i, height_j);
                corridorRoom.isCorridor = true; // Mark as corridor
                corridorRoom.name = $"Corridor {connection_room_i}-{connection_room_j}";
                corridorRoom.setColorFloor(highlight: false); // Set corridor color
                corridorRoom.neighbors = new();

                // connect the two rooms and the new corridor via connected_rooms lists
                corridorRoom.neighbors.Add(connection_room_i);
                corridorRoom.neighbors.Add(connection_room_j);
                int corridor_room_no = global.rooms.Count;
                corridorRoom.my_room_number = corridor_room_no;
                global.rooms.Add(corridorRoom); // add new corridor room to the master list
                global.rooms[connection_room_i].neighbors.Add(corridor_room_no);
                global.rooms[connection_room_j].neighbors.Add(corridor_room_no);

                // Remove second room (j) from unconnected rooms list
                for (var index = 0; index < unconnected_rooms.Count; index++)
                {
                    if (unconnected_rooms[index] == j)
                    {
                        unconnected_rooms.RemoveAt(index);
                        break; // found it, done removing j from unconnected rooms list
                    }
                }

                roomclass.DrawMapFromRoomsList(global.rooms);
                generator.DrawWalls();
                //yield return tm.YieldOrDelay(cfg.stepDelay / 3);
                if (tm.IfYield()) yield return null;
            }
            //DrawMapFromRoomsList(connected_rooms);
            //yield return StartCoroutine(generator.DrawWalls());
            if (tm.IfYield()) yield return null;
        }
        finally { if (local_tm) tm.End(); }
    }

    // ================= Helper functions ==========================

    // FindTwoClosestRooms does just that.  This way we connect rooms with short corridors first that are unlikely to
    // cross another room.
    // TODO: not very efficient, should use hashes
    public Vector2Int FindTwoClosestRooms(List<int> unconnected_rooms)
    {
        if (unconnected_rooms.Count < 2) return Vector2Int.zero; // not enough rooms

        Vector2Int closestPair = Vector2Int.zero;
        float minDistance = float.MaxValue;

        for (int i = 0; i < global.rooms.Count; i++)
        {
            if (!unconnected_rooms.Contains(i)) continue;  // i is not a unique room

            List<Vector2Int> room_cells_i = roomclass.get_union_of_connected_room_cells(i);
            Vector2Int center_i = corridors.GetCenterOfTiles(room_cells_i);

            for (int j = i + 1; j < global.rooms.Count; j++)
            {
                if (!unconnected_rooms.Contains(j)) continue;  // j is not a unique room

                List<Vector2Int> room_cells_j = roomclass.get_union_of_connected_room_cells(j);
                Vector2Int center_j = GetCenterOfTiles(room_cells_j);

                float distance = Vector2Int.Distance(center_i, center_j);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPair = new Vector2Int(i, j);
                }
            }
        }

        return closestPair;
    }

    // GetCeterOfTiles is used in finding a short corridor between unconnected rooms
    public Vector2Int GetCenterOfTiles(List<Vector2Int> tiles)
    {
        if (tiles.Count == 0) return new Vector2Int(0, 0);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var t in tiles)
        {
            if (t.x < minX) minX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.x > maxX) maxX = t.x;
            if (t.y > maxY) maxY = t.y;
        }

        return new Vector2Int(((minX + maxX) / 2), (minY + maxY) / 2);
    }

}
