using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace RagnarsRokare.SlaveGreylings
{
    public class MobGui : MonoBehaviour
    {
        public Transform m_inventoryRoot;
        public Text m_mobName;

        private static MobGui m_instance;
        public static MobGui instance => m_instance;

        private void Awake()
        {
            m_instance = this;
        }

        private void Update()
        {
            //bool @bool = m_animator.GetBool("visible");
            //if (!@bool)
            //{
            //    m_hiddenFrames++;
            //}
            Player localPlayer = Player.m_localPlayer;
            if (localPlayer == null || localPlayer.IsDead() || localPlayer.InCutscene() || localPlayer.IsTeleporting())
            {
                Hide();
                return;
            }
        }
        public void Hide()
        {

        }
    }
}
