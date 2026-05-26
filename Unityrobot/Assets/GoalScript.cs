using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoalScript : MonoBehaviour
{
 public SoccerAgent agent;
 public bool isEnemy;

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Ball"))
        {
            Debug.Log("goal touched");
            if (isEnemy == false) {
                agent.OnGoalScored();
            } else {
                agent.OnGoalSelfGoal();
            }
            
        }
    }
    
}
