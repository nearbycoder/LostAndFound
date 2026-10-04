#!/usr/bin/env bash
# Plays Monday's first case with simulated mouse and keyboard only, capturing each step to /tmp/laf/shots/t1_*.png.
P="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"; cd "$P"
q() { echo "$1" | Tools/ueval.sh | tail -1; }
Tools/dev_to_case.sh --bell >/dev/null || exit 1
Tools/click_obj.sh Bell click; sleep 0.5
q 'LostAndFound.Director.I.autoAdvance = true; return "auto";'
Tools/wait_member.sh LostAndFound.Director CanUseStamps true 60 >/dev/null
q 'LostAndFound.Director.I.autoAdvance = false; return "manual";'
Tools/editor_shot.sh t1_01_intro 0 >/dev/null
Tools/key.sh A; sleep 1.0
Tools/click_obj.sh Drawer_A click; sleep 1.4
Tools/click_obj.sh item:wallet_brown move; sleep 0.6
Tools/editor_shot.sh t1_02_tag 0 >/dev/null
Tools/click_obj.sh item:wallet_brown click; sleep 1.2
echo "held: $(q 'return LostAndFound.Desk.I.items["wallet_brown"].place.ToString();')"
Tools/editor_shot.sh t1_03_held 0 >/dev/null
Tools/click_obj.sh part:wallet_brown:Flap click; sleep 1.0
Tools/editor_shot.sh t1_04_open 0 >/dev/null
Tools/click_obj.sh hs:wallet_brown:photo move; sleep 0.5
Tools/editor_shot.sh t1_05_glint 0 >/dev/null
Tools/click_obj.sh hs:wallet_brown:photo click; sleep 0.6
Tools/editor_shot.sh t1_06_noted 0 >/dev/null
echo "discovered photo: $(q 'var w = LostAndFound.Desk.I.items["wallet_brown"]; return LostAndFound.Director.I.IsDiscovered(w.def, w.def.Detail("photo")) + "";')"
sleep 2.0
Tools/key.sh T; sleep 1.2
echo "on tray: $(q 'return (LostAndFound.Desk.I.OnTray != null) + "";')"
Tools/click_obj.sh ClaimSlip move; sleep 1.2
Tools/editor_shot.sh t1_07_slip 0 >/dev/null
Tools/click_obj.sh '(LostAndFound.Desk.I.props.stamps[0].transform.position)' click; sleep 1.0
Tools/click_obj.sh '(LostAndFound.ClaimSlip.I.transform.position + LostAndFound.ClaimSlip.I.transform.forward * -0.07f)' move; sleep 0.6
Tools/editor_shot.sh t1_08_stamp 0 >/dev/null
Tools/click_obj.sh '(LostAndFound.ClaimSlip.I.transform.position + LostAndFound.ClaimSlip.I.transform.forward * -0.07f)' click; sleep 0.25
Tools/editor_shot.sh t1_09_thump 0 >/dev/null
sleep 1.5
Tools/editor_shot.sh t1_10_receipt 0 >/dev/null
sleep 4
echo "record: $(q 'var r = LostAndFound.Director.I.State.Record("1.1"); return r == null ? "none" : r.verdict + "/" + r.grade + " " + r.ledger;')"
Tools/editor_shot.sh t1_11_after 0
