using Refraction.Logic.Events;
using Refraction.Presentation.Actor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    public class Door : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D collision)
        {
            if(collision != null)
            {
                var player = collision.GetComponent<PlayerController>();
                if(player != null )
                {
                    EventBus<RequestNextLevel>.Publish(new RequestNextLevel());
                }
            }
        }
    }
}
