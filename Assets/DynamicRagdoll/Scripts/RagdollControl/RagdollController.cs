﻿using UnityEngine;
using System.Collections.Generic;

/*
	Script for handling when to ragdoll, and switching between ragdolled and animations

	attach this script to the main character

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	
	while animated:

		hides ragdoll while teleporting it around to fit animation

		not set to kinematic, in order to save forces applied by collisions,
		but we disable gravity, and disable ragdoll self collisions

		we also ignore any collisions between the bones and any static colliders, 
		so the colliders dont jitter around while trying to teleport partway inside
		another collider

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	when ragdoll is initiated, 

		-	we store all the current positions of teh animated character

		-	adjust all the secondary transforms (fingers, etc..) on the ragdoll to match their masters
			(all the other transforms have been being teleported already)

		-	turn on gravity and enable ragdoll self collisions
		
		-	switch the renderrs to show the ragdoll that has the velocity of the animations


//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
						
	now we can have the leftover physics from the animation follow, without having to 
	worry too much about how closely the rigidbodies follow the actual animation
	(since we only use them when going ragdoll)

	after initiateing ragdoll we continue following the animated (but now invisible) master
	character, by setting the animated velocities, 
	
	we degenerate the following at a defined speed (which can be set at runtime)

		ragdollController.SetFallSpeed (running ? 2 : 5);
		ragdollController.GoRagdoll ();

	this will give us a smooth transition into giving unity complete physical control 
	over the ragdoll

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	Bone Decay
		by setting teh velocity of the bones when falling (to try and mimic the animation),
		we lose any external forces we had set towards the beginning of the fall

		in order to avoid this, set the bone decay for a bone to a value in the range (0,1).
		
		0 means it follows the animation fully (normal decay)
		1 means it ignores the animation completely and goes by the bones normal velocity

		bone decay for the bone's neighbors (which can be edited below) can be set as well.

		Example:

			RaycastHit hit;
			if (Physics.Raycast(ray, out hit)
			{
				//check if we hit a ragdoll bone
				RagdollBone bone = hit.transform.GetComponent<RagdollBone>();
				if (bone) {

					// check if the ragdoll has a controller
					if (bone.ragdoll.hasController) {
						RagdollController controller = bone.ragdoll.controller;

						float mainDecay = 1f;
						float neighborDecayMultiplier = .75f;

						// set bone decay for the hit bone, so the physics will affect it
						// slightly lower for neighbor bones
						controller.SetBoneDecay(bone.bone, mainDecay, neighborDecayMultiplier);

						//make it go ragdoll
						controller.GoRagdoll();					
					}

					//add force to it's rigidbody
					bone.rigidbody.AddForceAtPosition(ray.direction.normalized * bulletForce / Time.timeScale, hit.point, ForceMode.VelocityChange);
				}
			}


		Note: bone decay for collisions is implemented in CharacterCollisions.cs

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	when transitioning back to an animated state:
		
		- we play a get up animation

		- store the positions and rotations of the ragdolled bones
		
		- turn off physics for the ragdoll (no gravity, no static bone collisions)
		
		- linearly interpolate the positions and rotations from their ragdolled positions
		  to the animated positions (in late update)
		
		- when the blend lerp reaches 1, disable the ragdoll renderers
		  and enable the original master character model 
		  (which should still be playing the get up animation)

*/
namespace DynamicRagdoll {

	//Animator must have a humanoid avatar, figure out how to check this
	[RequireComponent(typeof(Animator))] 
	public class RagdollController : MonoBehaviour
	{	
		public event System.Action onGoRagdoll;
		public Ragdoll ragdoll;
		public RagdollControllerProfile profile;	

		[Tooltip("Don't get up anymore... 'dead'")]
		public bool disableGetUp;
		
		[HideInInspector] public RagdollControllerState state = RagdollControllerState.Animated;
		public bool ragdollRenderersEnabled { get { return state != RagdollControllerState.Animated; } }

		/*
			currently returns if we just entered the animated state, 
			theoretically it's always when we just blended to get up
		
			... or set up system to callback when animation is done
		*/
		public bool isGettingUp { get { return state == RagdollControllerState.Animated && timeSinceStateStart < getupTime; } }
		const float getupTime = 2.5f;
		float timeSinceStateStart { get { return Time.time - stateStartTime; } }

		
		Animator animator;
		Transform masterHips;				
		
		// Rotation of ragdollRootBone relative to transform.forward
		byte i;
		Quaternion rootboneToForward;
		bool calculatedRootboneToForward;

		Renderer[] masterRenderers;
		AnimatorCullingMode originalAnimatorCullingMode;

		float stateStartTime = -100;
		bool controllerInvalid { get { return profile == null || ragdoll == null; } }

		float fallDecay;
		float fallSpeed = -1;

		Dictionary<HumanBodyBones, float> boneDecays = new Dictionary<HumanBodyBones, float>();
		VelocityTracker[] animationVelocityTrackers;

		/*
			Configurable joint values for ragdoll bones
		*/
		static JointDrive jointDrive = new JointDrive();
		Quaternion[] startLocalRotations, localToJointSpaces;
		float[] lastTorques;



		//ignore pairs for blending bones ignorign static colliders
        HashSet<ColliderIgnorePair> blendBonesIgnoreStaticColliders = new HashSet<ColliderIgnorePair>();
        
		bool teleportingBones {
			get {
				return state == RagdollControllerState.Animated || state == RagdollControllerState.BlendToAnimated;
			}
		}

		/*
			Set the fall decay speed for the next 'Ragdolling'
		*/
		public void SetFallSpeed (float fallSpeed) {
			this.fallSpeed = fallSpeed;
		}

		/*
			enable or disable renderers on the master, and the ragdoll
		*/
		void EnableRenderers (bool masterEnabled, bool ragdollEnabled) {
			for (int i = 0; i < masterRenderers.Length; i++)
				masterRenderers[i].enabled = masterEnabled;

			ragdoll.EnableRenderers(ragdollEnabled);
		}
		
		void ChangeRagdollState (RagdollControllerState newState) {
			state = newState;
			//store the state change time
			stateStartTime = Time.time;
            
			//unignore the bones and static colliders that were ignored while blending and animating
			if (!teleportingBones) {
				EndStaticCollidersAndBoneCollisionIgnore();
			}
		}


		void GoRagdoll(object[] parameters) {
			GoRagdoll((float)parameters[1]);
		}
		void GoRagdoll(float delay=0) {
            EnableAfterDelay(GoRagdoll, delay, () => { GoRagdoll("from cue"); } );
        }
        
		
        /*
            self: the method calling this
        */
        System.Collections.IEnumerator EnableAfterDelay (System.Action<float> self, float delay) {
            yield return new WaitForSeconds(delay);
            self(0);
        }
        void EnableAfterDelay(System.Action<float> self, float delay, System.Action enableFN) {
            if (delay > 0) {
                StartCoroutine(EnableAfterDelay(self, delay));
                return;
            }
            enableFN();
        }




		/*
			call to start the ragdoll process
		*/
		public void GoRagdoll (string reason){

			if (!profile) {
				Debug.LogWarning("No Controller Profile on " + name);
				return;
			}
			if (!ragdoll) {
				Debug.LogWarning("No Ragdoll for " + name + " to control...");
				return;
			}
			
			if (state == RagdollControllerState.Falling || state == RagdollControllerState.Ragdolled)
				return;

			Debug.Log("went ragdoll for: " + reason);
			
			/*
				store start positions to begin calculating the velocity of the playing animation
			*/
			StoreAnimationStartPositions();

			fallDecay = 1;

			//teleport the whole ragdoll to fit the master
			ragdoll.TeleportToTarget(Ragdoll.TeleportType.SecondaryNonPhysicsBones);

			//enable physics for ragdoll 
			ragdoll.UseGravity(true);
			ragdoll.IgnoreSelfCollisions(false);
			
			//turn on ragdoll renderers, disable master renderers
			EnableRenderers(false, true);	

			// master renderers are disabled, but animation needs to play regardless
			// so dont cull
			animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

			//change the state
			ChangeRagdollState(RagdollControllerState.Falling);		

			if (onGoRagdoll != null) {
				onGoRagdoll();
			}

		}

		public event System.Action onEndGetUp, onBlendToAnim;
		public event System.Action onOrientateMaster;

		/*
			Transition from ragdolled to animated through the Blend state
		*/
		void StartGetUp () {

			//reset bone fall decay modifiers			
			ResetBoneDecays();

			//set fall speed to use default
			SetFallSpeed(-1);
			
			//disable physics affecting ragdoll
			ragdoll.UseGravity(false); 
			ragdoll.IgnoreSelfCollisions(true);

			//save the ragdoll positions
			ragdoll.SaveSnapshot();
			
			Vector3 ragdollHipsFwd = ragdoll.RootBone().transform.rotation * rootboneToForward * Vector3.forward;
			
			// Check if ragdoll is lying on its back or front
			bool onBack = Vector3.Dot(ragdollHipsFwd, Vector3.down) < 0f; 


			eventPlayer["OnFront"].SetValue(!onBack);
			AssetObjectsPacks.Playlist.InitializePerformance("getup", getUpEvent, eventPlayer, false, 1, new AssetObjectsPacks.MiniTransform( Vector3.zero, Quaternion.identity), true, onEndGetUp);

			
			// play get up animation
			//animator.SetTrigger(onBack ? "BackTrigger" : "FrontTrigger");

			ChangeRagdollState(RagdollControllerState.TeleportMasterToRagdoll);
		}
		public AssetObjectsPacks.Event getUpEvent;
		AssetObjectsPacks.EventPlayer eventPlayer;

		/*
			Here the master (this transform) gets reorientated to the ragdolls position
			which could have ended its fall in any direction and position
		*/
		void TeleportMasterToRagdoll () {		
			Transform ragdollHips = ragdoll.RootBone().transform;

			//calculate the rotation for the master root object (this)
			Quaternion rotation = ragdollHips.rotation * Quaternion.Inverse(masterHips.rotation) * transform.rotation;
			
			Vector3 fwd = rotation * Vector3.forward;
			fwd.y = 0;
			rotation = Quaternion.LookRotation(fwd);

			//calculate the position for the master root object (this)		
			Vector3 position = transform.position + (ragdollHips.position - masterHips.position);
			
			//Now cast a ray from the computed position downwards and find the floor
			RaycastHit hit;
			if (Physics.Raycast(new Ray(position + Vector3.up, Vector3.down), out hit, 5, profile.checkGroundMask)) {
				position.y = hit.point.y;
			}
						
			//set the position and rotation
			transform.rotation = rotation;
			transform.position = position;

			ChangeRagdollState(RagdollControllerState.BlendToAnimated);

			if (onOrientateMaster != null) {
				onOrientateMaster();
			}
		}

		/*
			In LateUpdate(), Mecanim has already updated the body pose according to the animations. 
			blend form the saved 'snapshot' ragdolled positions into the animated positions
		*/
		bool HandleBlendToAnimation () {
			
			// compute the ragdoll blend amount in the range 0...1
			float blend = timeSinceStateStart / profile.blendTime;
		
			if (blend > 1)
				blend = 1;
			
			ragdoll.LoadSnapshot(1-blend, true);

			// return true if we're done blending
			return blend == 1;
		}
		
		void ResetToAnimated () {

			
			// reset culling mode to original
			animator.cullingMode = originalAnimatorCullingMode;
			
			// turn off ragdoll renderers, enable master renderers
			EnableRenderers(true, false);

			//change state to animated
			ChangeRagdollState(RagdollControllerState.Animated);
		}

		/*
			Wait until transition to getUp animation is done,
			so the animation is lying down before teleporting 
			the master to the ragdoll rotation and position
		*/
		bool HandleWaitForMasterTeleport () {
			return timeSinceStateStart >= profile.orientateDelay;
		}

		/*
			teleport all ragdoll (and ragdoll parents) in order to match animation
			no need for secondary transforms since the ragdoll isnt being shown

			this way we can still 'shoot' or collide with the ragdoll
		*/
		void SimpleTeleportRagdollToMasterWhileAnimated () {
			ragdoll.TeleportToTarget(Ragdoll.TeleportType.PhysicsBonesAndParents);
		}

		void LateUpdate()
		{
			if (controllerInvalid) {
				return;	
			}
			InitializeForwardCalculation();
		
			switch (state) {
			
			case RagdollControllerState.Animated: 
				/*
					TRY UNCOMMENTING HERE IF WE'RE HAVING TROUBLE WITH PHYSICS DETECTION 
					OF RAGDOLL NOT DETECTING IK CHANGES
				*/
				SimpleTeleportRagdollToMasterWhileAnimated();
				break;
			case RagdollControllerState.TeleportMasterToRagdoll:
				if (HandleWaitForMasterTeleport()) {
					TeleportMasterToRagdoll();
				}
				break;
			case RagdollControllerState.BlendToAnimated:
				if (HandleBlendToAnimation()) {
					ResetToAnimated();
					if (onBlendToAnim != null) {
						onBlendToAnim();
					}
				}
				break;
			}

			UpdateLoop(Time.deltaTime);
		}


		void Awake () 
		{
			if (!profile) {
				Debug.LogWarning("No Controller Profile on " + name);
				return;
			}
			if (!ragdoll) {
				Debug.LogWarning("No Ragdoll for " + name + " to control...");
				return;
			}

			eventPlayer = GetComponent<AssetObjectsPacks.EventPlayer>();

			eventPlayer.AddParameter(new AssetObjectsPacks.CustomParameter( "OnFront", false) );
			
			animator = GetComponent<Animator>();
		
			// store original culling mode
			originalAnimatorCullingMode = animator.cullingMode;

			// store master hip bone
			masterHips = animator.GetBoneTransform(HumanBodyBones.Hips);
		
			// get all the master renderers to switch off when going ragdoll
			masterRenderers = GetComponentsInChildren<Renderer>();
					
			// set the ragdolls follow target (assumes same bone setup...)
			ragdoll.SetFollowTarget(animator);
			
			// tell the ragdoll it's being controlled
			ragdoll.SetController(this);

			// initialize animation following
			InitializeJointFollowing();
			InitializeVelocitySetValues();
			
			// disable physics
			ragdoll.UseGravity(false);
			//ragdoll.OverrideDepenetrationSpeed(-1);
			ragdoll.IgnoreSelfCollisions(true);

			ResetToAnimated();
			ResetBoneDecays();

			//subscribe to receive a callback on ragdoll bone collision
			ragdoll.onCollisionEnter += OnRagdollCollisionEnter;
		}
			
		/*
			initialize root bone forward calculation, for determining
			when ragdoll is on it's front or back (for getting up)
			
			Should have been done in Awake but mecanim does a strange initial rotation 
			for some models
		*/
		void InitializeForwardCalculation () {
			if (!calculatedRootboneToForward) {
				if (i == 2) {
					// Relative orientation of ragdollRootBone to ragdoll transform
					rootboneToForward = Quaternion.Inverse(masterHips.rotation) * transform.rotation; 
					calculatedRootboneToForward = true;
				}
				i++;
			}
		}
	
		void FixedUpdate ()		
		{
			if (controllerInvalid) {
				return;	
			}

			switch (state) {
			
			case RagdollControllerState.Ragdolled:

				if (disableGetUp == false) {
					//check the hips velocity to see if the ragdoll is still
					CheckForGetUp();
				}
				break;

			case RagdollControllerState.Animated:
				/*
					maybe move this to late update if we're not hitting transforms 
					that were edited on ik, as that happens after fixed update...
				*/
				SimpleTeleportRagdollToMasterWhileAnimated();
				break;

			case RagdollControllerState.Falling:
				/*
					if we're ragdolled and falling, set the calculated animation velocities we calculated in
					Late Update to the ragdoll bones, also handle joint targets
				*/
				SetPhysicsVelocities(Time.fixedDeltaTime);
				HandleFallLerp(Time.deltaTime);
				break;
			}
		}

		const float epsilon = .0001f;

		void HandleFallLerp (float deltaTime) {
			// Lerp follow force to zero from residual values
			float speed = (fallSpeed == -1 ? profile.fallDecaySpeed : fallSpeed) * deltaTime;
			
			if (fallDecay != 0) {
				fallDecay = Mathf.Lerp(fallDecay, 0, speed);

				if (fallDecay <= epsilon) {
					fallDecay = 0;
				}
			}
		}

		void CheckForGetUp () {
			//if we've spent enough time ragdolled
			if (Time.time - stateStartTime > profile.ragdollMinTime) {
				//if we're settled start get up
				if (ragdoll.RootBone().rigidbody.velocity.sqrMagnitude < profile.settledSpeed * profile.settledSpeed) {
					StartGetUp();
				}
			}
		}

		/*
			called in late update normally (in order to calculate animation velocities)
			in fixed update if using pd controller
		*/
		void UpdateLoop(float deltaTime)
		{
			if (state == RagdollControllerState.Falling) {		
				CalculateAnimationVelocities(deltaTime);
			}
		}

		/*
			checking after each physics follow method's update loop (where component values are set)
			in order to make sure that when it's 0, components are already set to their
			0 values when changing state
		*/
		void CheckForFallEnd (float fallLerp) {
			if (fallLerp == 0) {
				ChangeRagdollState(RagdollControllerState.Ragdolled);
			}
		}
			
		void InitializeVelocitySetValues () {

			animationVelocityTrackers = new VelocityTracker[Ragdoll.bonesCount];

			for (int i = 0; i < Ragdoll.bonesCount; i++) {
				
				Ragdoll.Element bone = ragdoll.GetBone(Ragdoll.humanBones[i]);
				
				//track position (offset by ragdoll bone's rigidbody centor of mass) of the follow target
				Vector3 massCenterOffset = bone.transform.InverseTransformPoint(bone.rigidbody.worldCenterOfMass);
				animationVelocityTrackers[i] = new VelocityTracker(bone.followTarget.transform, massCenterOffset);
			}
		}

		void InitializeJointFollowing () {

			startLocalRotations = new Quaternion[Ragdoll.bonesCount];
			localToJointSpaces = new Quaternion[Ragdoll.bonesCount];
			lastTorques = new float[Ragdoll.bonesCount];

			//skip first (hips dont have joints)
			for (int i = 1; i < Ragdoll.bonesCount; i++) {

				//get the ragdoll's joint
				ConfigurableJoint joint = ragdoll.GetBone(Ragdoll.humanBones[i]).joint;

				// set last torque to something that'll defineyl make it change
				lastTorques[i] = -1;

				//save rotation values for setting joint rotation
				localToJointSpaces[i] = Quaternion.LookRotation(Vector3.Cross (joint.axis, joint.secondaryAxis), joint.secondaryAxis);
				
				startLocalRotations[i] = joint.transform.localRotation * localToJointSpaces[i];

				localToJointSpaces[i] = Quaternion.Inverse(localToJointSpaces[i]);
				
				//set drive
				jointDrive = joint.slerpDrive;
				joint.slerpDrive = jointDrive;
			}
		}
		
		/*
			store the first positions to start calculating the velocity of the master animation
		*/
		void StoreAnimationStartPositions () {
			for (int i = 0; i < Ragdoll.bonesCount; i++) {	
				animationVelocityTrackers[i].Reset();
			}
		}
		
		/*
			Reset bone decays back to 0 for the next ragdolling
		*/
		void ResetBoneDecays () {
			for (int i = 0; i < Ragdoll.bonesCount; i++) {
				boneDecays[Ragdoll.humanBones[i]] = 0;
			}
		}
		
		
		/*
			Set the bone decay for the bone (in the range 0...1)
			0 = follow animation normally
			1 = dont follow animation at all
		*/
		public void SetBoneDecay (HumanBodyBones bones, float decayValue, float neighborMultiplier) {
			
			if (!boneDecays.ContainsKey(bones)) {
				Debug.LogError(bones + " is not a physics bone, cant set bone decay");
				return;
			}

			// making additive, so in case we hit a bone twice in a ragdoll session
			// it doesnt reset with a lower value
			
			float origDecay = boneDecays[bones];
			origDecay += decayValue;
			
			if (origDecay > 1)
				origDecay = 1;
			
			boneDecays[bones] = origDecay;

			if (neighborMultiplier > 0) {
				foreach (var n in profile.bones[Ragdoll.Bone2Index(bones)].neighbors) {
					SetBoneDecay(n, decayValue * neighborMultiplier, 0);
				}
			}
		}

		/*
			returns number with the largest magnitude
		*/
		static float MaxAbs(float a, float b) {
			return Mathf.Abs(a) > Mathf.Abs(b) ? a : b;
		}
		/*
			returns a vector with the largest components of each (based on absolute value)
		*/
		static Vector3 MaxAbs (Vector3 a, Vector3 b) {
			return new Vector3(MaxAbs(a.x, b.x), MaxAbs(a.y, b.y), MaxAbs(a.z, b.z));
		}
		
		/*
			set the velocities we calculated in late update for each animated bone			
			on the actual ragdoll
		*/
		void SetPhysicsVelocities (float deltaTime) {
			
			float dot = Vector3.Dot(ragdoll.RootBone().transform.up, Vector3.up);

			float maxVelocityForGravityAdd2 = profile.maxGravityAddVelocity * profile.maxGravityAddVelocity;

			float fallDecayCurveSample = 1f - fallDecay; //set up curves backwards... whoops
						
			// for each physics bone...
			for (int i = 0; i < Ragdoll.bonesCount; i++) {
				HumanBodyBones unityBone = Ragdoll.humanBones[i];
				
				Ragdoll.Element bone = ragdoll.GetBone(unityBone);

				Vector3 ragdollBoneVelocty = bone.rigidbody.velocity;

				// get the manually set bone decay value
				float boneDecay = boneDecays[unityBone];
			
				/*
					calculate the force decay based on the overall fall decay and the bone profile's
					fall force decay curve
				*/
				float forceDecay = Mathf.Clamp01(profile.bones[i].fallForceDecay.Evaluate (fallDecayCurveSample));
				
				//subtract manual decay
				forceDecay = Mathf.Clamp01(forceDecay - boneDecay);

				// if we're flipped to extremely, stop trying to follow anim
				// makes it look like it's 'gliding' forward in superman stance
				if (dot < profile.loseFollowDot)
					forceDecay = 0;

				// if we're still using some force to follow
				if (forceDecay != 0) {

					Vector3 animVelocity = animationVelocityTrackers[i].velocity;

					/*
						if animation velocity is below threshold magnitude, add some gravity to it
					*/
					if (animVelocity.sqrMagnitude < maxVelocityForGravityAdd2)
						animVelocity.y = Physics.gravity.y * deltaTime;

					/*
						if bone decay was manually set to make room for external velocities,
						use the most extreme component of each vector as the "target animated" velocity
					*/
					if (boneDecay != 0)
						animVelocity = MaxAbs(ragdollBoneVelocty, animVelocity);

					// set the velocity on the ragdoll rigidbody (based on the force decay)
					bone.rigidbody.velocity = Vector3.Lerp(ragdollBoneVelocty, animVelocity, forceDecay);
				}
			
				if (i != 0) {

					/*
						calculate the force decay based on the overall fall decay and the bone profile's					
						fall force decay curve
					*/
					float torqueDecay = Mathf.Clamp01(profile.bones[i].fallTorqueDecay.Evaluate (fallDecayCurveSample));
					
					//subtract manual decay
					torqueDecay = Mathf.Clamp01(torqueDecay - boneDecay);

					/*
						handle joints target for the ragdoll joints
					*/
					HandleJointFollow(bone, profile.maxTorque * torqueDecay, i);
				}
			}

			CheckForFallEnd(fallDecay);						
		}


		/* 
			Calculate the velocity (per bone) of the current playing animation

			called in LateUpdate since we're using the animated character for these
			calculations
		*/
		void CalculateAnimationVelocities (float deltaTime) {
			if (fallDecay != 0) {

				float reciprocalDeltaTime = 1f / deltaTime;
				
				// for each physics bone...
				for (int i = 0; i < Ragdoll.bonesCount; i++) {
					animationVelocityTrackers[i].TrackVelocity(reciprocalDeltaTime, false);
				}
			}
		}

		/*
			Set the bone's configurable joint target to the master local rotation
		*/
		void HandleJointFollow (Ragdoll.Element bone, float torque, int boneIndex) {
			if (!bone.joint) 
				return;
					
			//setting joint torque every frame was slow, so check here if its changed
			if (torque != lastTorques[boneIndex]) {
				lastTorques[boneIndex] = torque;

				jointDrive.positionSpring = torque;
				bone.joint.slerpDrive = jointDrive;
			}

			//set joints target rotation		
			if (torque != 0) {
				Quaternion targetLocalRotation = bone.followTarget.transform.localRotation;
				bone.joint.targetRotation = localToJointSpaces[boneIndex] * Quaternion.Inverse(targetLocalRotation) * startLocalRotations[boneIndex];
			}	
		}


		void EndStaticCollidersAndBoneCollisionIgnore () {
			//unignore the bones and static colliders that were ignored while blending and animating

			if (blendBonesIgnoreStaticColliders.Count > 0) {
			
				foreach(var p in blendBonesIgnoreStaticColliders)
					p.EndIgnore();
			
				blendBonesIgnoreStaticColliders.Clear();
			}
		}



		/*
            when blending to animation, ignore collisons wiht static colliders that bones 
            come in contact with (this eliminates jitters from bone teleport trying to go through
            the static collider).  

            bone rigidbodies cannot be kinematic though, or else collisions wont exert forces in time
            (when ragdollign on collision)
        */
        void CheckForBlendIgnoreStaticCollision (RagdollBone bone, Collision collision) {
            // if wee're blending or animated (teleporting bones)
            if (teleportingBones) {
                //if it's static
                if (collision.collider.attachedRigidbody == null){   

                    blendBonesIgnoreStaticColliders.Add(new ColliderIgnorePair(bone.boneCollider, collision.collider));
                }
            }
        }

		/*
			callback called when ragdoll bone gets a collision
		*/    
		void OnRagdollCollisionEnter(RagdollBone bone, Collision collision)
		{
            CheckForBlendIgnoreStaticCollision(bone, collision);
		}
	}
}