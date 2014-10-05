using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CCPrac2.NetChange
{
    /// <summary>
    /// Handles the connection to neighbouring processes.
    /// Also keeps state for that neighbour.
    /// </summary>
    class ConnectionWorker
    {
        private int id;
        private int port; // Differs from "id" because we get assigned a separate handler port

        public ConnectionWorker(int id)
        {
            this.id = id;
        }

        /// <summary>
        /// Starts probing for a connection.
        /// </summary>
        public void Start()
        {

        }
    }
}
