using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;
using UnityEditor.MemoryProfiler;
using Unity.Collections;

// ==================================================================

public partial class DungeonGenerator : MonoBehaviour
{

    [HideInInspector] public bool success;    // global generic return value from various tasks
    [HideInInspector] public string failure;    // global failure description string
    

    // =======================================================
    // helper routines for rect_int room descriptions

    // None, since this is a ScatterRooms specific structure and not used in RoomGrowthAlgorithms

    // =======================================================


    // =======================================================
    //  Extra functions

    public String ListOfIntToString(List<int> ilist, bool do_sort = true)
    {
        String result = "List: ";
        if (do_sort) ilist.Sort();
        foreach (int i in ilist)
        {
            result = result + i + ",";
        }
        return result;
    }

    public Vector2Int[] directions_xy = { Vector2Int.up,
                                   Vector2Int.down,
                                   Vector2Int.left,
                                   Vector2Int.right,
                                   Vector2Int.up + Vector2Int.left,
                                   Vector2Int.up + Vector2Int.right,
                                   Vector2Int.down + Vector2Int.left,
                                   Vector2Int.down + Vector2Int.right };

    public Color getColor(Color? color = null, bool highlight = true, string rgba = "")
    {
        Color colorrgba = new(); //temp
        Color return_color = Color.white;

        if (color != null)
            return_color = (Color)color;
        else if ((!string.IsNullOrEmpty(rgba)) && (ColorUtility.TryParseHtmlString(rgba, out colorrgba)))
            return_color = colorrgba;
        else if (highlight)
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f);   // Bright Random
        else // highlight == false
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.1f, 0.4f); // Dark Random

        return return_color;
    }

}