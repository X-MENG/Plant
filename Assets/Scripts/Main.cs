using UnityEngine;

public class Main : MonoBehaviour
{
    // Use this for initialization
    void Start()
    {
        //PlantParser plantParser = new PlantParser();
        //plantParser.Init("plant9.json");
        string loginStr = "s < 3";
        string poExp = Util.ToPoLogicExp(loginStr);
        Util.DBG(poExp);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
