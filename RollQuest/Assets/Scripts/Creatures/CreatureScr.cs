using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreatureScr : MonoBehaviour
{
    public enum Group
    {
        Anomaly, 
        Goblin, 
        Wolves, 
        CrystalKin, 
        Tideclaw, 
        Hiveborn, 
        Chameleodon, 
    }
    
    public Group creatureGroup;

    public int baseHealth;
    public int currentHealth;
    public int baseDamage;
    public int baseDefense;
    public int currentDefense;
    public int baseSpeed;
    public int baseAttackRange;
    public int baseDetectionRange;
    public int baseIgnoreRange;
}
