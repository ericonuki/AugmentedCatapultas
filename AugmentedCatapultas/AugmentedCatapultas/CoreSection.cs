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

        Scene scene;
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        SpriteFont textFont;

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

            // Display the mouse cursor
            this.IsMouseVisible = true;
            // Initialize the GoblinXNA framework
            State.InitGoblin(graphics, Content, "");
            // Initialize the scene graph
            scene = new Scene();
            // Set the background color to CornflowerBlue color.
            // GraphicsDevice.Clear(...) is called by
            //Scene object with this color.
            scene.BackgroundColor = Color.CornflowerBlue;
            // Custom method for creating a 3D object
            CreateObject();
            // Set up the lights used in the scene
            CreateLights();
            // Set up the camera, which defines the eye location //and viewing frustum
            CreateCamera();
            // Show Frames-Per-Second on the screen for debugging
            State.ShowFPS = true;

            scene.PhysicsEngine = new NewtonPhysics();

            // For some reason, it sometimes causes memory conflict when it attempts to update the
            // marker transformation in the multi-threaded code, so if you see weird exceptions 
            // thrown in Shaders, then you should not enable the marker tracking thread
            State.ThreadOption = (ushort)ThreadOptions.MarkerTracking;

            // Enable shadow mapping
            // NOTE: In order to use shadow mapping, you will need to add 'MultiLightShadowMap.fx'
            // and 'SimpleShadowShader.fx' shader files to your 'Content' directory. Also in here,
            // we're creating the ShadowMap before the creation of 3D objects since we need to assign
            // this ShadowMap to the IShadowShader used for the 3D objects
            scene.ShadowMap = new MultiLightShadowMap();

            // Set up optical marker tracking
            // Note that we don't create our own camera when we use optical marker
            // tracking. It'll be created automatically
            SetupMarkerTracking();

            // Set up the lights used in the scene
            CreateLights();

            // Create 3D objects
            CreateObjects();

            // Create the ground that represents the physical ground marker array
            CreateGround();

            // Show Frames-Per-Second on the screen for debugging
            State.ShowFPS = true;
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
            lightNode.LightProjection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, 1, 1f, 500);

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
            GeometryNode groundNode = new GeometryNode("Ground");

            // We will use TexturedBox instead of regular Box class since we will need the
            // texture coordinates elements for passing the vertices to the SimpleShadowShader
            // we will be using
            groundNode.Model = new TexturedBox(324, 180, 0.1f);

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
        }

        private void CreateObjects()
        {
            // Create a geometry node with a model of a sphere that will be overlaid on
            // top of the ground marker array
            GeometryNode sphereNode = new GeometryNode("Sphere");
            // We will use TexturedSphere instead of regular Box class since we will need the
            // texture coordinates elements for passing the vertices to the SimpleShadowShader
            // we will be using
            sphereNode.Model = new TexturedSphere(16, 20, 20);

            // Add this sphere model to the physics engine for collision detection
            sphereNode.AddToPhysicsEngine = true;
            sphereNode.Physics.Shape = ShapeType.Sphere;
            // Make this sphere model cast and receive shadows
            sphereNode.Model.ShadowAttribute = ShadowAttribute.ReceiveCast;
            // Assign a shadow shader for this model that uses the IShadowMap we assigned to the scene
            sphereNode.Model.Shader = new SimpleShadowShader(scene.ShadowMap);

            // Create a marker node to track a ground marker array.
            groundMarkerNode = new MarkerNode(scene.MarkerTracker, "ALVARGroundArray.xml");

            TransformNode sphereTransNode = new TransformNode();
            sphereTransNode.Translation = new Vector3(0, 0, 50);

            // Create a material to apply to the sphere model
            Material sphereMaterial = new Material();
            sphereMaterial.Diffuse = new Vector4(0, 0.5f, 0, 1);
            sphereMaterial.Specular = Color.White.ToVector4();
            sphereMaterial.SpecularPower = 10;

            sphereNode.Material = sphereMaterial;

            // Now add the above nodes to the scene graph in the appropriate order.
            // Note that only the nodes added below the marker node are affected by 
            // the marker transformation.
            scene.RootNode.AddChild(groundMarkerNode);
            groundMarkerNode.AddChild(sphereTransNode);
            sphereTransNode.AddChild(sphereNode);

            // Create a geometry node with a model of a box that will be overlaid on
            // top of the ground marker array initially. (When the toolbar marker array is
            // detected, it will be overlaid on top of the toolbar marker array.)
            boxNode = new GeometryNode("Box");
            // We will use TexturedBox instead of regular Box class since we will need the
            // texture coordinates elements for passing the vertices to the SimpleShadowShader
            // we will be using
            boxNode.Model = new TexturedBox(32.4f);

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

            // Create a collision pair and add a collision callback function that will be
            // called when the pair collides
            NewtonPhysics.CollisionPair pair = new NewtonPhysics.CollisionPair(boxNode.Physics, sphereNode.Physics);
            ((NewtonPhysics)scene.PhysicsEngine).AddCollisionCallback(pair, BoxSphereCollision);
        }

        private void BoxSphereCollision(NewtonPhysics.CollisionPair pair)
        {
            Console.WriteLine("Box and Sphere has collided");
        }

        private void CreateObject()
        {
            // Create a transform node to define the
            // transformation of this sphere
            // (Transformation includes translation, rotation,
            // and scaling)
            TransformNode sphereTransNode = new TransformNode();

            // We want to scale the sphere by half in all three
            // dimensions and translate the sphere 5 units back
            // along the Z axis
            sphereTransNode.Scale = new Vector3(0.5f, 0.5f, 0.5f);
            sphereTransNode.Translation = new Vector3(0, 0, -5);

            // Create a geometry node with a model of a sphere
            // NOTE: We strongly recommend you give each geometry
            // node a unique name for use with
            // the physics engine and networking; if you leave
            // out the name, it will be automatically generated
            GeometryNode sphereNode = new GeometryNode("Sphere");
            sphereNode.Model = new Sphere(3, 60, 60);

            // Create a material to apply to the sphere model
            Material sphereMaterial = new Material();
            sphereMaterial.Diffuse = new Vector4(0.5f, 0, 0, 1);
            sphereMaterial.Specular = Color.White.ToVector4();
            sphereMaterial.SpecularPower = 10;

            // Apply this material to the sphere model
            sphereNode.Material = sphereMaterial;

            // Child nodes are affected by parent nodes. In this
            // case, we want to make
            // the sphere node have the transformation, so we add
            // the transform node to
            // the root node, and then add the sphere node to the
            // transform node.
            scene.RootNode.AddChild(sphereTransNode);
            sphereTransNode.AddChild(sphereNode);
        }

        private void CreateCamera()
        {
            // Create a camera
            Camera camera = new Camera();

            // Put the camera at the origin
            camera.Translation = new Vector3(0, 0, 0);

            // Set the vertical field of view
            // to be 60 degrees
            camera.FieldOfViewY = MathHelper.ToRadians(60);

            // Set the near clipping plane to be
            // 0.1f unit away from the camera
            camera.ZNearPlane = 0.1f;

            // Set the far clipping plane to be
            // 1000 units away from the camera
            camera.ZFarPlane = 1000;

            // Now assign this camera to a camera node,
            // and add this camera node to our scene graph
            CameraNode cameraNode = new CameraNode(camera);
            scene.RootNode.AddChild(cameraNode);

            // Assign the camera node to be our
            // scene graph's current camera node
            scene.CameraNode = cameraNode;
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

            scene.Update(gameTime.ElapsedGameTime, gameTime.IsRunningSlowly, this.IsActive);

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here

            // Draw a 2D text string at the center of the
            // screen.
            UI2DRenderer.WriteText(Vector2.Zero, "Hello World!!", Color.GreenYellow, textFont,
                GoblinEnums.HorizontalAlignment.Center, GoblinEnums.VerticalAlignment.Center);

            // Draw the scene graph
            scene.Draw(gameTime.ElapsedGameTime, gameTime.IsRunningSlowly);

            base.Draw(gameTime);
        }
    }
}
