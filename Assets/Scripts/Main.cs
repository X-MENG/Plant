﻿using UnityEngine;

public class Main : MonoBehaviour
{
    // Use this for initialization
    void Start()
    {
        PlantParser plantParser = new PlantParser();
        plantParser.Init("plant10.json");
    }

    // Update is called once per frame
    void Update()
    {

    }
}
