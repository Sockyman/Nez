﻿using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Nez.Textures;
using System.IO;


namespace Nez.Particles
{
	public class ParticleEmitter : RenderableComponent
	{
		public override float width { get { return 5f; } }
		public override float height { get { return 5f; } }

		public bool isPaused { get { return _isPaused; } }
		public bool isPlaying { get { return _active && !_isPaused; } }
		public bool isStopped { get { return !_active && !_isPaused; } }
		public float elapsedTime { get { return _elapsedTime; } }

		/// <summary>
		/// config object with various properties to deal with particle collisions
		/// </summary>
		public ParticleCollisionConfig collisionConfig;

		/// <summary>
		/// keeps track of how many particles should be emitted
		/// </summary>
		float _emitCounter;

		/// <summary>
		/// tracks the elapsed time of the emitter
		/// </summary>
		float _elapsedTime;

		bool _active = false;
		bool _isPaused;

		/// <summary>
		/// if the emitter is emitting this will be true. Note that emitting can be false while particles are still alive. emitting gets set
		/// to false and then any live particles are allowed to finish their lifecycle.
		/// </summary>
		bool _emitting;
		List<Particle> _particles;
		bool _playOnAwake;
		ParticleEmitterConfig _emitterConfig;


		public ParticleEmitter( ParticleEmitterConfig emitterConfig, bool playOnAwake = true )
		{
			_emitterConfig = emitterConfig;
			_playOnAwake = playOnAwake;
			_particles = new List<Particle>( (int)_emitterConfig.maxParticles );
			QuickCache<Particle>.warmCache( (int)_emitterConfig.maxParticles );

			// set some sensible defaults
			collisionConfig.bounce = 0f;
			collisionConfig.collidesWithLayers = Physics.AllLayers;
			collisionConfig.dampen = 0f;
			collisionConfig.gravity = new Vector2( 0f, 100f );
			collisionConfig.lifetimeLoss = 0f;
			collisionConfig.minKillSpeed = float.MaxValue;
			collisionConfig.radiusScale = 0.5f;

			initialize();
		}


		/// <summary>
		/// creates the SpriteBatch and loads the texture if it is available
		/// </summary>
		void initialize()
		{
			// prep our custom BlendState and set the RenderState with it
			var blendState = new BlendState();
			blendState.ColorSourceBlend = blendState.AlphaSourceBlend = _emitterConfig.blendFuncSource;
			blendState.ColorDestinationBlend = blendState.AlphaDestinationBlend = _emitterConfig.blendFuncDestination;

			renderState = new RenderState( blendState );
		}


		#region Component/RenderableComponent

		public override void onAwake()
		{
			if( _playOnAwake )
				play();
		}


		public override void update()
		{
			if( _isPaused )
				return;
			
			// if the emitter is active and the emission rate is greater than zero then emit particles
			if( _active && _emitterConfig.emissionRate > 0 )
			{
				var rate = 1.0f / _emitterConfig.emissionRate;

				if( _particles.Count < _emitterConfig.maxParticles )
					_emitCounter += Time.deltaTime;

				while( _emitting && _particles.Count < _emitterConfig.maxParticles && _emitCounter > rate )
				{
					addParticle();
					_emitCounter -= rate;
				}

				_elapsedTime += Time.deltaTime;

				if( _emitterConfig.duration != -1 && _emitterConfig.duration < _elapsedTime )
				{
					// when we hit our duration we dont emit any more particles
					_emitting = false;

					// once all our particles are done we stop the emitter
					if( _particles.Count == 0 )
						stop();
				}
			}

			// prep data for the particle.update method
			var rootPosition = renderPosition;

			// loop through all the particles updating their location and color
			for( var i = _particles.Count - 1; i >= 0; i-- )
			{
				// get the current particle and update it
				var currentParticle = _particles[i];

				// if update returns true that means the particle is done
				if( currentParticle.update( _emitterConfig, ref collisionConfig, rootPosition ) )
				{
					QuickCache<Particle>.push( currentParticle );
					_particles.RemoveAt( i );
				}
			}
		}


		public override void render( Graphics graphics, Camera camera )
		{
			// we still render when we are paused
			if( !_active && !_isPaused )
				return;

			var rootPosition = renderPosition;

			// loop through all the particles updating their location and color
			for( var i = 0; i < _particles.Count; i++ )
			{
				var currentParticle = _particles[i];

				if( _emitterConfig.subtexture == null )
					graphics.spriteBatch.Draw( graphics.pixelTexture, rootPosition + currentParticle.position, graphics.pixelTexture.sourceRect, currentParticle.color, currentParticle.rotation, Vector2.One, currentParticle.particleSize * 0.5f, SpriteEffects.None, layerDepth );
				else
					graphics.spriteBatch.Draw( _emitterConfig.subtexture, rootPosition + currentParticle.position, _emitterConfig.subtexture.sourceRect, currentParticle.color, currentParticle.rotation, _emitterConfig.subtexture.center, currentParticle.particleSize / _emitterConfig.subtexture.sourceRect.Width, SpriteEffects.None, layerDepth );
			}
		}


		public override void debugRender( Graphics graphics )
		{
			// we still render when we are paused
			if( !_active && !_isPaused )
				return;

			var rootPosition = renderPosition;

			// loop through all the particles updating their location and color
			for( var i = 0; i < _particles.Count; i++ )
			{
				var currentParticle = _particles[i];
				//graphics.spriteBatch.drawCircle( rootPosition + currentParticle.position, currentParticle.particleSize * 0.5f, Color.IndianRed );
				graphics.spriteBatch.drawHollowRect( rootPosition + currentParticle.position - new Vector2( currentParticle.particleSize * 0.5f, currentParticle.particleSize * 0.5f ), currentParticle.particleSize, currentParticle.particleSize, Color.IndianRed );
			}
		}

		#endregion


		/// <summary>
		/// removes all particles from the particle emitter
		/// </summary>
		public void clear()
		{
			for( var i = 0; i < _particles.Count; i++ )
				QuickCache<Particle>.push( _particles[i] );
			_particles.Clear();
		}


		/// <summary>
		/// plays the particle emitter
		/// </summary>
		public void play()
		{
			// if we are just unpausing, we only toggle flags and we dont mess with any other parameters
			if( _isPaused )
			{
				_active = true;
				_isPaused = false;
				return;
			}

			_active = true;
			_emitting = true;
			_elapsedTime = 0;
			_emitCounter = 0;
		}


		/// <summary>
		/// stops the particle emitter
		/// </summary>
		public void stop()
		{
			_active = false;
			_isPaused = false;
			_elapsedTime = 0;
			_emitCounter = 0;
			clear();
		}


		/// <summary>
		/// pauses the particle emitter
		/// </summary>
		public void pause()
		{
			_isPaused = true;
			_active = false;
		}


		/// <summary>
		/// manually emit some particles
		/// </summary>
		/// <param name="count">Count.</param>
		public void emit( int count )
		{
			initialize();
			_active = true;
			for( var i = 0; i < count; i++ )
				addParticle();
		}


		/// <summary>
		/// adds a Particle to the emitter
		/// </summary>
		void addParticle()
		{
			// take the next particle out of the particle pool we have created and initialize it
			var particle = QuickCache<Particle>.pop();
			particle.initialize( _emitterConfig );
			_particles.Add( particle );
		}

	}
}
