using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

using GoblinXNA;
using GoblinXNA.Graphics;
using GoblinXNA.SceneGraph;
using Model = GoblinXNA.Graphics.Model;
using GoblinXNA.Graphics.Geometry;
using GoblinXNA.Device.Generic;
using GoblinXNA.UI.UI2D;

using GoblinXNA.Device.Capture;
using GoblinXNA.Device.Vision;
using GoblinXNA.Device.Vision.Marker;
using GoblinXNA.Device.Util;
using GoblinXNA.Physics;

using GoblinXNA.Physics.Newton1;

using GoblinXNA.Helpers;
using GoblinXNA.Shaders;

namespace AugmentedCatapultas
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class CoreSection : Microsoft.Xna.Framework.Game
    {
        int collisionCount;

        List<Element> updatables;

        Tiro p1Tiro;

        Scene scene;
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        SpriteFont textFont;
        GeometryNode groundNode;

        MarkerNode groundMarkerNode, toolbarMarkerNode;
        GeometryNode boxNode;
        bool useStaticImage = false;

        public CoreSection()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            //Content.RootDirectory = "AugmentedCatapultas\\AugmentedCatapultasContent";

            graphics.PreferredBackBufferWidth = 1024;
            graphics.PreferredBackBufferHeight = 768;
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();

            updatables = new List<Element>();

            // Display the mouse cursor
            this.IsMouseVisible = true;

            // Initialize the GoblinXNA framework
            State.InitGoblin(graphics, Content, "");

            // Initialize the scene graph
            scene = new Scene();

            // Use the newton physics engine to perform collision detection
            scene.PhysicsEngine = new NewtonPhysics();

            // For some reason, it sometimes causes memory conflict when it attempts to update the
            // marker transformation in the multi-threaded code, so if you see weird exceptions 
            // thrown in Shaders, then you should not enable the marker tracking thread
            State.ThreadOption = (ushort)ThreadOptions.MarkerTracking;

            // Set up optical marker tracking
            // Note that we don't create our own camera when we use optical marker
            // tracking. It'll be created automatically
            SetupMarkerTracking();

            // Set up the lights used in the scene
            CreateLights();

            // Enable shadow mapping
            // NOTE: In order to use shadow mapping, you will need to add 'MultiLightShadowMap.fx'
            // and 'SimpleShadowShader.fx' shader files to your 'Content' directory. Also in here,
            // we're creating the ShadowMap before the creation of 3D objects since we need to assign
            // this ShadowMap to the IShadowShader used for the 3D objects
            scene.ShadowMap = new MultiLightShadowMap();

            // Create 3D objects
            CreateObjects();

            // Create the ground that represents the physical ground marker array
            CreateGround();

            // Show Frames-Per-Second on the screen for debugging
            State.ShowFPS = true;

            collisionCount = 0;
            // Set up physics material interaction specifications between the shooting box and the ground
            NewtonMaterial physMat = new NewtonMaterial();
            physMat.MaterialName1 = "CannonBall";
            physMat.MaterialName2 = "Ground";
            physMat.Elasticity = 0.7f;
            physMat.StaticFriction = 0.8f;
            physMat.KineticFriction = 0.2f;
            // Define a callback function that will be called when the two materials contact/collide
            physMat.ContactProcessCallback = delegate(Vector3 contactPosition, Vector3 contactNormal,
                float contactSpeed, float colObj1ContactTangentSpeed, float colObj2ContactTangentSpeed,
                Vector3 colObj1ContactTangentDirection, Vector3 colObj2ContactTangentDirection)
            {
                if (contactSpeed > 2)
                    collisionCount++;

                // When a cube box collides with the ground, it can have more than 1 contact points
                // depending on the collision surface, so we only play sound and add 3D texts once
                // every four contacts to avoid multiple sound play or text addition for one surface
                // contact
                if (collisionCount >= 4)
                {

                    // Reset the count
                    collisionCount = 0;
                }
            };

            // Add this physics material interaction specifications to the physics engine
            ((NewtonPhysics)scene.PhysicsEngine).AddPhysicsMaterial(physMat);
        }

        private void CreateLights()
        {
            // Create a directional light source
            LightSource lightSource = new LightSource();
            lightSource.Direction = new Vector3(1, -1, -1);
            lightSource.Diffuse = Color.White.ToVector4();
            lightSource.Specular = new Vector4(0.6f, 0.6f, 0.6f, 1);

            // Create a light node to hold the light source
            LightNode lightNode = new LightNode();
            lightNode.LightSource = lightSource;

            // Set this light node to cast shadows (by just setting this to true will not cast any shadows,
            // scene.ShadowMap needs to be set to a valid IShadowMap and Model.Shader needs to be set to
            // a proper IShadowShader implementation
            lightNode.CastShadows = true;

            // You should also set the light projection when casting shadow from this light
            lightNode.LightProjection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4,
                1, 1f, 500);

            scene.RootNode.AddChild(lightNode);
        }

        private void SetupMarkerTracking()
        {
            IVideoCapture captureDevice = null;

            if (useStaticImage)
            {
                captureDevice = new NullCapture();
                captureDevice.InitVideoCapture(0, FrameRate._30Hz, Resolution._800x600,
                    ImageFormat.R8G8B8_24, false);
                ((NullCapture)captureDevice).StaticImageFile = "MarkerImage";
            }
            else
            {
                // Create our video capture device that uses DirectShow library. Note that 
                // the combinations of resolution and frame rate that are allowed depend on 
                // the particular video capture device. Thus, setting incorrect resolution 
                // and frame rate values may cause exceptions or simply be ignored, depending 
                // on the device driver.  The values set here will work for a Microsoft VX 6000, 
                // and many other webcams.
                captureDevice = new DirectShowCapture();
                captureDevice.InitVideoCapture(0, FrameRate._60Hz, Resolution._640x480,
                    ImageFormat.R8G8B8_24, false);
            }

            // Add this video capture device to the scene so that it can be used for
            // the marker tracker
            scene.AddVideoCaptureDevice(captureDevice);

            ALVARMarkerTracker tracker = new ALVARMarkerTracker();
            tracker.MaxMarkerError = 0.02f;
            tracker.InitTracker(captureDevice.Width, captureDevice.Height, "calib.xml", 32.4f);

            // Set the marker tracker to use for our scene
            scene.MarkerTracker = tracker;

            // Display the camera image in the background. Note that this parameter should
            // be set after adding at least one video capture device to the Scene class.
            scene.ShowCameraImage = true;
        }

        private void CreateGround()
        {
            groundNode = new GeometryNode("Ground");

            // We will use TexturedBox instead of regular Box class since we will need the
            // texture coordinates elements for passing the vertices to the SimpleShadowShader
            // we will be using
            groundNode.Model = new TexturedBox(800, 600, 0.1f);

            // Set this ground model to act as an occluder so that it appears transparent
            groundNode.IsOccluder = true;

            // Make the ground model to receive shadow casted by other objects with
            // ShadowAttribute.ReceiveCast
            groundNode.Model.ShadowAttribute = ShadowAttribute.ReceiveOnly;
            // Assign a shadow shader for this model that uses the IShadowMap we assigned to the scene
            groundNode.Model.Shader = new SimpleShadowShader(scene.ShadowMap);

            Material groundMaterial = new Material();
            groundMaterial.Diffuse = Color.Gray.ToVector4();
            groundMaterial.Specular = Color.White.ToVector4();
            groundMaterial.SpecularPower = 20;

            groundNode.Material = groundMaterial;

            groundMarkerNode.AddChild(groundNode);

            groundNode.AddToPhysicsEngine = true;
            groundNode.Physics.Shape = ShapeType.Box;
            groundNode.Physics.Collidable = true;
            //groundNode.Physics.MaterialName = "Ground";
            groundNode.Physics.ApplyGravity = false;
            groundNode.Physics.Interactable = true;
            groundNode.Physics.Manipulatable = true;
            groundNode.Physics.Mass = 1;
            groundNode.Physics.Pickable = true;

            NewtonPhysics.CollisionPair CannonGroundColisionPair = new NewtonPhysics.CollisionPair(p1Tiro.obj.Physics, groundNode.Physics);

            ((NewtonPhysics)scene.PhysicsEngine).AddCollisionCallback(CannonGroundColisionPair, ballBounceGround);
        }

        private void CreateObjects()
        {
            
            // Create a marker node to track a ground marker array.
            groundMarkerNode = new MarkerNode(scene.MarkerTracker, "ALVARGroundArray.xml");
            

            // Now add the above nodes to the scene graph in the appropriate order.
            // Note that only the nodes added below the marker node are affected by 
            // the marker transformation.
            scene.RootNode.AddChild(groundMarkerNode);
            
            // Create a geometry node with a model of a box that will be overlaid on
            // top of the ground marker array initially. (When the toolbar marker array is
            // detected, it will be overlaid on top of the toolbar marker array.)
            boxNode = new GeometryNode("Box");
            // We will use TexturedBox instead of regular Box class since we will need the
            // texture coordinates elements for passing the vertices to the SimpleShadowShader
            // we will be using
            boxNode.Model = new TexturedBox(100f,100f,5);

            // Add this box model to the physics engine for collision detection
            boxNode.AddToPhysicsEngine = true;
            boxNode.Physics.Shape = ShapeType.Box;
            // Make this box model cast and receive shadows
            boxNode.Model.ShadowAttribute = ShadowAttribute.ReceiveCast;
            // Assign a shadow shader for this model that uses the IShadowMap we assigned to the scene
            boxNode.Model.Shader = new SimpleShadowShader(scene.ShadowMap);

            // Create a marker node to track a toolbar marker array.
            toolbarMarkerNode = new MarkerNode(scene.MarkerTracker, "ALVARToolbar.xml");

            scene.RootNode.AddChild(toolbarMarkerNode);

            // Create a material to apply to the box model
            Material boxMaterial = new Material();
            boxMaterial.Diffuse = new Vector4(0.5f, 0, 0, 1);
            boxMaterial.Specular = Color.White.ToVector4();
            boxMaterial.SpecularPower = 10;

            boxNode.Material = boxMaterial;

            // Add this box model node to the ground marker node
            groundMarkerNode.AddChild(boxNode);
            
            p1Tiro = new Tiro("p1Tiro", new Vector3(100, 0, 100), 1, 20, scene,groundMarkerNode);
            groundMarkerNode.AddChild(p1Tiro.objTransfNode);
            updatables.Add(p1Tiro);

            NewtonPhysics.CollisionPair CannonPlayerColisionPair = new NewtonPhysics.CollisionPair(p1Tiro.obj.Physics, boxNode.Physics);

            ((NewtonPhysics)scene.PhysicsEngine).AddCollisionCallback(CannonPlayerColisionPair, playerShot);
            
        }

        private void playerShot(NewtonPhysics.CollisionPair pair)
        {
            Vector3 scaleBox;
            Quaternion rotationBox;
            Vector3 translationBox;
            pair.CollisionObject2.PhysicsWorldTransform.Decompose(out scaleBox, out rotationBox, out translationBox);

            Vector3 scaleSphere;
            Quaternion rotationSphere;
            Vector3 translationSphere;
            pair.CollisionObject1.PhysicsWorldTransform.Decompose(out scaleSphere, out rotationSphere, out translationSphere);

            Vector3 racket_direction = Vector3.Transform(new Vector3(0, 0, 1), rotationBox);

            Vector3 resulting_direction = Vector3.Transform(racket_direction, Quaternion.Inverse(rotationSphere));

            p1Tiro.addImpulse_using_Mass_and_Velocity(1, resulting_direction / 10);

            p1Tiro.animationStartedApplyGravity = true;
        }

        private void ballBounceGround(NewtonPhysics.CollisionPair pair)
        {
            p1Tiro.bounceCount++;
            if (p1Tiro._momentum.Z < 0)
            {
                Vector3 impulseForceWithDrag = new Vector3(p1Tiro._momentum.X * -0.3f, p1Tiro._momentum.Y * -0.3f, p1Tiro._momentum.Z * -1.7f);
                p1Tiro._momentum += impulseForceWithDrag;
                if (p1Tiro._momentum.Z < 0.2)
                {
                    p1Tiro._momentum.Z = 0;
                }
            }
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
            textFont = Content.Load<SpriteFont>("Sample");

            // TODO: use this.Content to load your game content here
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            Content.Unload();
            // TODO: Unload any non ContentManager content here
        }

        /// This is called when the game terminates.
        protected override void Dispose(bool disposing)
        {
            scene.Dispose();
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            // TODO: Add your update logic here

            foreach (Element obj in updatables)
            {
                obj.updatePosition(groundMarkerNode);
                if (p1Tiro.animationStartedApplyGravity)
                {
                    p1Tiro.addImpulse_using_Force(new Vector3(0, 0, -0.85f), gameTime.ElapsedGameTime.Milliseconds);
                }
            }
            
            scene.Update(gameTime.ElapsedGameTime, gameTime.IsRunningSlowly, this.IsActive);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            // If ground marker array is detected
            if (groundMarkerNode.MarkerFound)
            {
                // If the toolbar marker array is detected, then overlay the box model on top
                // of the toolbar marker array; otherwise, overlay the box model on top of
                // the ground marker array
                if (toolbarMarkerNode.MarkerFound)
                {
                    // The box model is overlaid on the ground marker array, so in order to
                    // make the box model appear overlaid on the toolbar marker array, we need
                    // to offset the ground marker array's transformation. Thus, we multiply
                    // the toolbar marker array's transformation with the inverse of the ground marker
                    // array's transformation, which becomes T*G(inv)*G = T*I = T as a result, 
                    // where T is the transformation of the toolbar marker array, G is the 
                    // transformation of the ground marker array, and I is the identity matrix. 
                    // The Vector3(0, 0, 16.1) is a shift translation to make the box overlaid right 
                    // on top of the toolbar marker. The top-left corner of the left marker of the 
                    // toolbar marker array is defined as (0, 0, 0), so in order to make the box model
                    // appear right on top of the left marker of the toolbar marker array, we shift by
                    // half of each dimension of the 8x8x8 box model.  The approach used here requires that
                    // the ground marker array remains visible at all times.
                    Vector3 shiftVector = new Vector3(0, 0, 5f);
                    Matrix mat = Matrix.CreateTranslation(shiftVector) *
                        toolbarMarkerNode.WorldTransformation *
                        Matrix.Invert(groundMarkerNode.WorldTransformation);

                    // Modify the transformation in the physics engine
                    ((NewtonPhysics)scene.PhysicsEngine).SetTransform(boxNode.Physics, mat);
                }
                else
                    ((NewtonPhysics)scene.PhysicsEngine).SetTransform(boxNode.Physics,
                        Matrix.CreateTranslation(0, 0, 5f));
            }

            scene.Draw(gameTime.ElapsedGameTime, gameTime.IsRunningSlowly);
        }

        public HandleMouseClick MouselickHandler { get; set; }
    }
}
