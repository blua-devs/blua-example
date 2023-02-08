using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using bLua;

public class Statistics : MonoBehaviour
{
    class StatisticsRow
    {
        public Func<string> GetContent;
        public StatisticsRow(Func<string> _contentFunc)
        {
            GetContent = _contentFunc;
        }
    }

    float boxWidth = 200f;
    float rowHeight = 20f;
    float spaceBetweenRows = 2f;

    List<StatisticsRow> rows = new List<StatisticsRow>()
    {
        new StatisticsRow(() => "Instance Count: " + bLuaInstance.GetInstanceCount().ToString())
    };

    public static Statistics instance;


    private void Awake()
    {
        if (instance != null)
        {
            MonoBehaviour.Destroy(this.gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(Screen.width - boxWidth - 10f, Screen.height - 30f - (rows.Count * (rowHeight + spaceBetweenRows)) - 15f, boxWidth, 20f + (rows.Count * (rowHeight + spaceBetweenRows)) + 15f), "bLua Statistics");

        for (int i = 0; i < rows.Count; i++)
        {
            GUI.Label(new Rect(Screen.width - boxWidth - 10f + 10f, Screen.height - 10f - (rows.Count * (rowHeight + spaceBetweenRows)) + (i * (rowHeight + spaceBetweenRows)) - 5f, boxWidth - 20f, rowHeight), rows[i].GetContent());
        }
    }
}
