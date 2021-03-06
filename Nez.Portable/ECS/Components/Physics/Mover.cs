﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Nez
{
	/// <summary>
	/// helper class illustrating one way to handle movement taking into account all Collisions including triggers. The ITriggerListener
	/// interface is used to manage callbacks to any triggers that are breached while moving. An object must move only via the Mover.move
	/// method for triggers to be properly reported. Note that multiple Movers interacting with each other will end up calling ITriggerListener
	/// multiple times.
	/// </summary>
	public class Mover : Component
	{
		ColliderTriggerHelper _triggerHelper;

		public override void OnAddedToEntity()
		{
			_triggerHelper = new ColliderTriggerHelper(Entity);
		}

        /// <summary>
        /// caculates the movement modifying the motion vector to take into account any collisions that will
        /// occur when moving
        /// </summary>
        /// <returns><c>true</c>, if movement was calculated, <c>false</c> otherwise.</returns>
        /// <param name="motion">Motion.</param>
        /// <param name="collisionResult">Collision result.</param>
        public bool CalculateMovement(ref Vector2 motion, out CollisionResult collisionResult)
        {
            collisionResult = new CollisionResult();

            // no collider? just move and forget about it
            if (Entity.GetComponent<Collider>() == null || _triggerHelper == null)
                return false;

            // 1. move all non-trigger Colliders and get closest collision
            var colliders = Entity.GetComponents<Collider>();
            for (var i = 0; i < colliders.Count; i++)
            {
                var collider = colliders[i];

                // skip triggers for now. we will revisit them after we move.
                if (collider.IsTrigger)
                    continue;


				// check for any collisions along the line to the destination
				bool colliding = collider.CollidesWithAny(out var _);
				CheckRaycast(collider, ref motion, ref collisionResult);


				// fetch anything that we might collide with at our new position
				var bounds = collider.Bounds;
                bounds.X += motion.X;
                bounds.Y += motion.Y;
                var neighbors =
                    Physics.BoxcastBroadphaseExcludingSelf(collider, ref bounds, collider.CollidesWithLayers);

                foreach (var neighbor in neighbors)
                {
                    // skip triggers for now. we will revisit them after we move.
                    if (neighbor.IsTrigger)
                        continue;

                    if (collider.CollidesWith(neighbor, motion, out CollisionResult _InternalcollisionResult))
                    {
                        // hit. back off our motion
                        motion -= _InternalcollisionResult.MinimumTranslationVector;

                        // If we hit multiple objects, only take on the first for simplicity sake.
                        if (_InternalcollisionResult.Collider != null)
                            collisionResult = _InternalcollisionResult;
                    }
                }


				// auxilery check just in case
				if (!colliding && CheckRaycast(collider, ref motion, ref collisionResult))
				{
					motion = new Vector2();
				}
			}

            ListPool<Collider>.Free(colliders);

            return collisionResult.Collider != null;
        }

		/// <summary>
		/// Checks for any collisions a colliser will have following a motion vector and modifies the vector to account for them.
		/// </summary>
		/// <param name="collider"></param>
		/// <param name="motion"></param>
		/// <param name="collisionResult"></param>
		/// <returns>Whether or not there was a collision</returns>
		private bool CheckRaycast(Collider collider, ref Vector2 motion, ref CollisionResult collisionResult)
		{
			var cPosition = collider.AbsolutePosition;
			if (cPosition == new Vector2(float.NaN, float.NaN))
				return false;
			var cDestination = collider.AbsolutePosition + motion;
			var rayHit = Physics.Linecast(cPosition, cDestination);
			if (rayHit.Collider != null)
			{
				collisionResult.MinimumTranslationVector = motion - (rayHit.Point - cPosition);
				motion = (rayHit.Point - cPosition);
				collisionResult.Collider = rayHit.Collider;
				collisionResult.Normal = rayHit.Normal;
				collisionResult.Point = rayHit.Point;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Calculates the movement modifying the motion vector to take into account any collisions that will
		/// occur when moving. This version is modified to output through a given collection to show every
		/// collision that occured.
		/// </summary>
		/// <returns>The amount of collisions that occured.</returns>
		/// <param name="motion">Motion.</param>
		/// <param name="collisionResult">Collision result.</param>
		public int AdvancedCalculateMovement(ref Vector2 motion, ICollection<CollisionResult> collisionResults)
        {
            int Collisions = 0;
            // no collider? just move and forget about it
            if (Entity.GetComponent<Collider>() == null || _triggerHelper == null)
                return Collisions;

            // 1. move all non-trigger Colliders and get closest collision
            var colliders = Entity.GetComponents<Collider>();
            for (var i = 0; i < colliders.Count; i++)
            {
                var collider = colliders[i];

                // skip triggers for now. we will revisit them after we move.
                if (collider.IsTrigger)
                    continue;

                // fetch anything that we might collide with at our new position
                var bounds = collider.Bounds;
                bounds.X += motion.X;
                bounds.Y += motion.Y;
                var neighbors =
                    Physics.BoxcastBroadphaseExcludingSelf(collider, ref bounds, collider.CollidesWithLayers);

                foreach (var neighbor in neighbors)
                {
                    // skip triggers for now. we will revisit them after we move.
                    if (neighbor.IsTrigger)
                        continue;

                    if (collider.CollidesWith(neighbor, motion, out CollisionResult _InternalcollisionResult))
                    {
                        // hit. back off our motion
                        motion -= _InternalcollisionResult.MinimumTranslationVector;
                        collisionResults.Add(_InternalcollisionResult);

                        Collisions++;
                    }
                }
            }

            ListPool<Collider>.Free(colliders);

            return Collisions;
        }

        /// <summary>
        /// applies the movement from calculateMovement to the entity and updates the triggerHelper
        /// </summary>
        /// <param name="motion">Motion.</param>
        public void ApplyMovement(Vector2 motion)
		{
			// 2. move entity to its new position if we have a collision else move the full amount. motion is updated when a collision occurs
			Entity.Transform.Position += motion;

			// 3. do an overlap check of all Colliders that are triggers with all broadphase colliders, triggers or not.
			//    Any overlaps result in trigger events.
			_triggerHelper?.Update();
		}

		/// <summary>
		/// moves the entity taking collisions into account by calling calculateMovement followed by applyMovement;
		/// </summary>
		/// <returns><c>true</c>, if move actor was newed, <c>false</c> otherwise.</returns>
		/// <param name="motion">Motion.</param>
		/// <param name="collisionResult">Collision result.</param>
		public bool Move(Vector2 motion, out CollisionResult collisionResult)
		{
			CalculateMovement(ref motion, out collisionResult);

			ApplyMovement(motion);

			return collisionResult.Collider != null;
		}
	}
}