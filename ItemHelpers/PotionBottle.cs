﻿using GameNetcodeStuff;
using LiquidLabyrinth.Enums;
using LiquidLabyrinth.Utilities;
using LiquidLabyrinth.Utilities.MonoBehaviours;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace LiquidLabyrinth.ItemHelpers
{
    class PotionBottle : Throwable
    {
        private Dictionary<float, string> data = null;
        private Renderer rend;
        [Header("Wobble Settings")]
        Vector3 lastPos;
        Vector3 velocity;
        Vector3 lastRot;
        Vector3 angularVelocity;
        public float MaxWobble = 1f;
        //private float MaxWobbleBase = 1f;
        public float WobbleSpeed = 1f;
        public float Recovery = 1f;
        private float _localFill = -1f;
        float wobbleAmountX;
        float wobbleAmountZ;
        float wobbleAmountToAddX;
        float wobbleAmountToAddZ;
        float pulse;
        float time = 0.5f;

        [Space(3f)]
        [Header("Bottle Properties")]
        public Animator itemAnimator;

        public AudioClip openCorkSFX;

        public AudioClip closeCorkSFX;

        public AudioClip changeModeSFX;

        public AudioClip glassBreakSFX;
        public AudioClip liquidShakeSFX;

        public GameObject Liquid;
        public List<string> BottleProperties; // TODO: Add bottle properties class. API in mind, change `string` into the class.
        string bottleType; // for debugging only
        [Space(3f)]
        [Header("Liquid Properties")]
        public bool BreakBottle = false;
        private NetworkVariable<int> net_playerHeldByInt = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> net_Fill = new NetworkVariable<float>(-1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> net_isOpened = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<Color> net_color = new NetworkVariable<Color>(Color.red, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<Color> net_lighterColor = new NetworkVariable<Color>(Color.blue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<BottleModes> net_mode = new NetworkVariable<BottleModes>(BottleModes.Open, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private int _BottleModesLength = Enum.GetValues(typeof(BottleModes)).Length;
        public string GetModeString(BottleModes mode, bool isOpened)
        {
            if (mode == BottleModes.Open && isOpened)
            {
                return "Close";
            }
            else
            {
                return Enum.GetName(typeof(BottleModes), mode);
            }
        }

        public override void EquipItem()
        {
            base.EquipItem();
            playerHeldBy.equippedUsableItemQE = true;
            wobbleAmountToAddX = wobbleAmountToAddX + UnityEngine.Random.Range(1f, 10f);
            wobbleAmountToAddZ = wobbleAmountToAddZ + UnityEngine.Random.Range(1f, 10f);
            if (IsOwner)
            {
                net_playerHeldByInt.Value = (int)playerHeldBy.playerClientId;
            }
        }

        public override void DiscardItem()
        {
            base.DiscardItem();
            if (IsOwner)
            {
                net_playerHeldByInt.Value = -1;
            }
        }

        public override void InteractItem()
        {
            base.InteractItem();
            Plugin.Logger.LogWarning("Interacted!");
        }


        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            LMBToThrow = false;
            Plugin.Logger.LogWarning($"{used} {buttonDown}");
            Holding = buttonDown;
            if (playerHeldBy == null) return;
            playerHeldBy.activatingItem = buttonDown;
            // Gotta make this a handler somehow.
            switch (net_mode.Value)
            {
                case BottleModes.Open:
                    if (!buttonDown) break;
                    // This is in reverse because we will set the value to this after the logic is done. (If a desync happens, the way this is set up with no RPCS, its gonna fucking explode planet earth)
                    itemAnimator.SetBool("CorkOpen", !net_isOpened.Value);
                    if (!net_isOpened.Value)
                    {
                        itemAudio.PlayOneShot(openCorkSFX);
                    }
                    else
                    {
                        itemAudio.PlayOneShot(closeCorkSFX);
                    }
                    // Do this last to not cause any network issues lmao.
                    if (IsOwner)
                    {
                        net_isOpened.Value = !net_isOpened.Value;
                        SetControlTipsForItem();
                    }
                    break;
                case BottleModes.Drink:
                    if (!net_isOpened.Value) break;
                    playerHeldBy.playerBodyAnimator.SetBool("useTZPItem", buttonDown);
                    //itemAnimator.SetBool("Drink", buttonDown);
                    break;
                case BottleModes.Throw:
                    LMBToThrow = true;
                    playerThrownBy = playerHeldBy;
                    if (IsOwner && UnityEngine.Random.Range(1, 100) <= 25) BreakBottle = true;
                    break;
                case BottleModes.Toast:
                    if (!buttonDown || !IsOwner) break;
                    if (ToastCoroutine != null) break;
                    ToastCoroutine = StartCoroutine(Toast());
                    break;
                case BottleModes.Shake:
                    if (!buttonDown) break;
                    CoroutineHandler.Instance.NewCoroutine(ShakeBottle());
                    break;
            }
            base.ItemActivate(used, buttonDown);
        }

        private IEnumerator ShakeBottle()
        {
            while (Holding)
            {
                playerHeldBy.playerBodyAnimator.SetTrigger("shakeItem");
                AnimatorStateInfo stateInfo = playerHeldBy.playerBodyAnimator.GetCurrentAnimatorStateInfo(2);
                if (!stateInfo.IsName("ShakeItem"))
                {
                    yield return null;
                    continue;
                }
                float animationDuration = stateInfo.length;
                float _start = UnityEngine.Random.Range(0, liquidShakeSFX.length - animationDuration);
                float _stop = Mathf.Min(_start + animationDuration, liquidShakeSFX.length);
                AudioClip subclip = liquidShakeSFX.MakeSubclip(_start, _stop);
                itemAudio.PlayOneShot(subclip);
                CoroutineHandler.Instance.NewCoroutine(itemAudio.FadeOut(1f));
                wobbleAmountToAddX = wobbleAmountToAddX + UnityEngine.Random.Range(1f, 10f);
                wobbleAmountToAddZ = wobbleAmountToAddZ + UnityEngine.Random.Range(1f, 10f);
                yield return new WaitForSeconds(animationDuration-0.35f);
            }
            yield break;
        }

        // this shit don't work.
        private Coroutine ToastCoroutine;
        private IEnumerator Toast()
        {
            RaycastHit[] hits = Physics.SphereCastAll(new Ray(playerHeldBy.gameplayCamera.transform.position + playerHeldBy.gameplayCamera.transform.forward * 20f, playerHeldBy.gameplayCamera.transform.forward), 20f, 80f, LayerMask.GetMask("Props"));
            if (hits.Count() > 0)
            {
                RaycastHit found = hits.FirstOrDefault(hit => hit.transform.TryGetComponent(out PotionBottle bottle) && bottle.playerHeldBy != GameNetworkManager.Instance.localPlayerController);// && bottle.isHeld && bottle.playerHeldBy != GameNetworkManager.Instance.localPlayerController
                Plugin.Logger.LogWarning(found.transform);
                if (found.transform != null && playerHeldBy != null)
                {
                    Plugin.Logger.LogWarning("FOUND!");
                    if (IsOwner) playerHeldBy.UpdateSpecialAnimationValue(true, 0, playerHeldBy.targetYRot, true);
                    PlayerUtils.RotateToObject(playerHeldBy, found.transform.gameObject);
                    playerHeldBy.inSpecialInteractAnimation = true;
                    playerHeldBy.isClimbingLadder = false;
                    playerHeldBy.playerBodyAnimator.ResetTrigger("SA_ChargeItem");
                    playerHeldBy.playerBodyAnimator.SetTrigger("SA_ChargeItem");
                    EnablePhysics(false);
                    yield return new WaitForSeconds(0.5f);
                    EnablePhysics(true);
                    playerHeldBy.playerBodyAnimator.ResetTrigger("SA_ChargeItem");
                    if (IsOwner) playerHeldBy.UpdateSpecialAnimationValue(false, 0, 0f, false);
                    playerHeldBy.activatingItem = false;
                    playerHeldBy.inSpecialInteractAnimation = false;
                    playerHeldBy.isClimbingLadder = false;
                    Plugin.Logger.LogWarning("DONE ANIMATION");
                }
            }
            ToastCoroutine = null;
            yield break;
        }

        public override void ItemInteractLeftRight(bool right)
        {
            base.ItemInteractLeftRight(right);
            if (playerHeldBy != null && playerHeldBy.activatingItem) return;
            if (!IsOwner) return;
            if (right)
            {
                net_mode.Value -= 1;
                if ((int)net_mode.Value == -1) net_mode.Value = (BottleModes)(_BottleModesLength - 1);
            }
            else
            {
                net_mode.Value += 1;
                if ((int)net_mode.Value == _BottleModesLength) net_mode.Value = 0;
            }
            itemAudio.PlayOneShot(changeModeSFX);
            SetControlTipsForItem();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            floatWhileOrbiting = true;
            if (IsHost || IsServer)
            {
                scrapValue = itemProperties.creditsWorth;
                ScanNodeProperties nodeProperties = GetComponentInChildren<ScanNodeProperties>();
                if (nodeProperties != null)
                {
                    if (nodeProperties.headerText == "BottleType")
                    {
                        nodeProperties.headerText += UnityEngine.Random.Range(1, 12);
                        Plugin.Logger.LogWarning("generating random name");
                    }
                    bottleType = nodeProperties.headerText;
                    nodeProperties.subText = $"Value: {itemProperties.creditsWorth}";
                }
                int NameHash = Math.Abs(nodeProperties.headerText.GetHashCode());
                float r = (NameHash * 16807) % 256 / 255f; // Pseudo-random number generator
                float g = ((NameHash * 48271) % 256) / 255f; // Pseudo-random number generator
                float b = ((NameHash * 69621) % 256) / 255f; // Pseudo-random number generator
                float lightenValue = 1f; // Adjust this value to control how much the color is lightened
                net_lighterColor.Value = new Color(Mathf.Clamp01(r + lightenValue),
                                              Mathf.Clamp01(g + lightenValue),
                                              Mathf.Clamp01(b + lightenValue));
                net_color.Value = new(r, g, b, 1);
                if (_localFill != -1)
                {
                    net_Fill.Value = _localFill;
                }
                if (net_Fill.Value == -1)
                {
                    net_Fill.Value = UnityEngine.Random.Range(0f, 1f);
                    Plugin.Logger.LogWarning("Bottle fill is -1, setting random value.");
                }
            }
            // ^ This part above only runs on server to sync initialization.
            if (net_playerHeldByInt.Value != -1)
            {
                playerHeldBy = StartOfRound.Instance.allPlayerScripts[net_playerHeldByInt.Value];
            }
            // Sync to all clients.
            gameObject.GetComponent<MeshRenderer>().material.color = new Color(net_color.Value.g, net_color.Value.r, net_color.Value.b);
            if (Liquid != null)
            {
                rend = Liquid.GetComponent<MeshRenderer>();
                rend.material.SetColor("_LiquidColor", net_color.Value);
                rend.material.SetColor("_SurfaceColor", net_lighterColor.Value);
                rend.material.SetFloat("_Fill", net_Fill.Value);
            }
            itemAnimator.SetBool("CorkOpen", net_isOpened.Value);
        }

        public override void LoadItemSaveData(int saveData)
        {
            base.LoadItemSaveData(saveData);
            Plugin.Logger.LogWarning($"LoadItemSaveData called! Got: {saveData}");
            _localFill = (saveData / 100f);
            if (!NetworkManager.Singleton.IsHost || !NetworkManager.Singleton.IsServer) return; // Return if not host or server.
            if (ES3.KeyExists("shipBottleData", GameNetworkManager.Instance.currentSaveFileName) && data == null)
            {
                data = ES3.Load<Dictionary<float, string>>("shipBottleData", GameNetworkManager.Instance.currentSaveFileName);
            }
            if (data == null) return;
            float key = saveData + Math.Abs(transform.position.x + transform.position.y + transform.position.z);
            if (data.TryGetValue(key, out string value))
            {
                GetComponentInChildren<ScanNodeProperties>().headerText = value;
                Plugin.Logger.LogWarning($"Found data: {value} ({key})");
            }
            else
            {
                Plugin.Logger.LogWarning($"Couldn't find save data for {GetType().Name}. ({key}). Please send this log to the mod developer.");
            }
        }



        public override int GetItemDataToSave()
        {
            Dictionary<float, string> data = new Dictionary<float, string>();
            data.Add((int)(net_Fill.Value * 100f) + Math.Abs(transform.position.x + transform.position.y + transform.position.z), GetComponentInChildren<ScanNodeProperties>().headerText);
            SaveUtils.AddToQueue<PotionBottle>(GetType(), data);
            return (int)(net_Fill.Value * 100f);
        }


        [ServerRpc(RequireOwnership = false)]
        void HitGround_ServerRpc()
        {
            HitGround_ClientRpc();
        }

        [ClientRpc]
        void HitGround_ClientRpc()
        {
            Plugin.Logger.LogWarning("It hit da ground");


            // REVIVE TEST:
            RaycastHit[] hits = Physics.SphereCastAll(new Ray(gameObject.transform.position + gameObject.transform.up * 2f, gameObject.transform.forward), 10f, 80f, 1048576);

            //END OF REVIVE TEST
            if (hits.Count() > 0 && Plugin.Instance.RevivePlayer.Value)
            {
                foreach (RaycastHit hit in hits)
                {
                    if (hit.transform.TryGetComponent(out DeadBodyInfo deadBodyInfo))
                    {
                        PlayerControllerB player = deadBodyInfo.playerScript;
                        PlayerUtils.RevivePlayer(player, deadBodyInfo, hit.transform.position);
                    }
                }
            }


            // TODO: Glass shatter effect and puddle instansiation
            AudioSource.PlayClipAtPoint(glassBreakSFX, gameObject.transform.position);
            AudioSource.PlayClipAtPoint(itemProperties.dropSFX, gameObject.transform.position);
            if (IsServer || IsHost) Destroy(gameObject, .5f);
        }

        public override void OnCollisionEnter(Collision collision)
        {
            LMBToThrow = false;
            if (isThrown.Value && BreakBottle)
            {
                HitGround_ServerRpc();
            }
            base.OnCollisionEnter(collision);
        }


        // Debugging this
        public override void SetControlTipsForItem()
        {
            base.SetControlTipsForItem();
            string[] allLines;
            string modeString = GetModeString(net_mode.Value, net_isOpened.Value);
            allLines = new string[]{
                $"BottleType: {bottleType}",
                $"Mode: {modeString}"
            };
            if (IsOwner)
            {
                HUDManager.Instance.ChangeControlTipMultiple(allLines, true, itemProperties);
            }
        }

        public override void Update()
        {
            base.Update();
            if (itemUsedUp) return;


            MaxWobble = net_Fill.Value * 0.2f;
            if (IsOwner && playerHeldBy != null && net_mode.Value == BottleModes.Drink && Holding && net_isOpened.Value && playerHeldBy.playerBodyAnimator.GetCurrentAnimatorStateInfo(2).normalizedTime > 1)
            {
                net_Fill.Value = Mathf.Lerp(net_Fill.Value, 0f, Time.deltaTime * 0.5f);
                Plugin.Logger.LogWarning($"Started drinking! ({net_Fill.Value})");
            }
            if (net_Fill.Value <= 0.05) // fun
            {
                if (IsOwner)
                {
                    net_Fill.Value = 0f;
                    rend.material.SetFloat("_Fill", 0f);
                }
                itemUsedUp = true;
                return;
            }

            rend.material.SetFloat("_Fill", net_Fill.Value);
            Wobble();
        }

        private void Wobble()
        {
            time += Time.deltaTime;
            // decrease wobble over time
            wobbleAmountToAddX = Mathf.Lerp(wobbleAmountToAddX, 0, Time.deltaTime * (Recovery));
            wobbleAmountToAddZ = Mathf.Lerp(wobbleAmountToAddZ, 0, Time.deltaTime * (Recovery));

            // make a sine wave of the decreasing wobble
            pulse = 2 * Mathf.PI * WobbleSpeed;
            wobbleAmountX = wobbleAmountToAddX * Mathf.Sin(pulse * time);
            wobbleAmountZ = wobbleAmountToAddZ * Mathf.Sin(pulse * time);

            // send it to the shader
            rend.material.SetFloat("_WobbleX", wobbleAmountX);
            rend.material.SetFloat("_WobbleZ", wobbleAmountZ);

            // velocity
            velocity = (lastPos - transform.position) / Time.deltaTime;
            angularVelocity = transform.rotation.eulerAngles - lastRot;


            // add clamped velocity to wobble
            wobbleAmountToAddX += Mathf.Clamp((velocity.x + (angularVelocity.z * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);
            wobbleAmountToAddZ += Mathf.Clamp((velocity.z + (angularVelocity.x * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);

            // keep last position
            lastPos = transform.position;
            lastRot = transform.rotation.eulerAngles;
        }
    }
}
