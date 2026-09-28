namespace CurePlease
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Forms;
    using System.Xml.Linq;
    using CurePlease.Game;
    using static Form1;

    public partial class PartyBuffs : Form
    {
        private Form1 f1;

        public class BuffList
        {
            public string ID { get; set; }
            public string Name { get; set; }
        }

        public List<BuffList> XMLBuffList = new List<BuffList>();

        public PartyBuffs(Form1 f)
        {
            this.StartPosition = FormStartPosition.CenterScreen;

            InitializeComponent();

            f1 = f;

            if (f1.setinstance2.Enabled == true)
            {
                // Create the required List

                // Buff names: Resources/Buffs.xml when it's present (it shipped with the old release
                // download, not the source), otherwise the StatusEffect names built into the app.
                string buffsFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Buffs.xml");
                if (System.IO.File.Exists(buffsFile))
                {
                    try
                    {
                        foreach (XElement BuffElement in XElement.Load(buffsFile).Elements("o"))
                        {
                            XMLBuffList.Add(new BuffList() { ID = (string)BuffElement.Attribute("id"), Name = (string)BuffElement.Attribute("en") });
                        }
                    }
                    catch (Exception)
                    {
                        XMLBuffList.Clear();
                    }
                }

                if (XMLBuffList.Count == 0)
                {
                    foreach (StatusEffect effect in Enum.GetValues(typeof(StatusEffect)))
                    {
                        XMLBuffList.Add(new BuffList() { ID = ((short)effect).ToString(), Name = effect.ToString().Replace('_', ' ') });
                    }
                }
            }
            else
            {
                MessageBox.Show("No character was selected as the power leveler, this can not be opened yet.");
            }
        }

        private void update_effects_Tick(object sender, EventArgs e)
        {
            ailment_list.Text = "";

            // Copy the list first: the cortana receive thread replaces entries while this window reads.
            List<BuffStorage> snapshot;
            lock (f1.ActiveBuffs)
            {
                snapshot = f1.ActiveBuffs.ToList();
            }

            // Search through current active party buffs
            foreach (BuffStorage ailment in snapshot)
            {
                // First add Character name and a Line Break.
                ailment_list.AppendText(ailment.CharacterName.ToUpper() + "\n");

                // Now create a list and loop through each buff and name them
                List<string> named_buffs = ailment.CharacterBuffs.Split(',').ToList();

                int i = 1;
                int count = named_buffs.Count();

                foreach (string acBuff in named_buffs)
                {
                    i++;

                    var found_Buff = XMLBuffList.Find(r => r.ID == acBuff);

                    if (found_Buff != null)
                    {
                        if (i == count)
                        {
                            ailment_list.AppendText(found_Buff.Name + " (" + acBuff + ") ");
                        }
                        else
                        {
                            ailment_list.AppendText(found_Buff.Name + " (" + acBuff + "), ");
                        }
                    }
                }

                ailment_list.AppendText("\n\n");
            }
        }
    }
}
