using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RagnarsRokare.MobAI
{
    public class TestBehaviour : MonsterAI
    {
        public void Mimik(MonsterAI original)
        {
            this.Copy(original);
        }

        public ZNetView NView { get { return m_nview; } }
    }
}
