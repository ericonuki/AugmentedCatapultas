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
    class Tiro:Element
    {
        private DateTime lastTimePositionWasUpdated; // Quando foi a última vez que esse negócio foi atualizado na tela? Era bom olhar né?
        public Vector3 _momentum; // Pois não vou mudar a referencia do tiro. Não consigo pensar COMO fazer isso agora. Vai ser sempre em relação ao chão. Eu sei que é só aplicar a matriz de transformação invertida e bla bla bla.
        public Vector3 _position;
        public int mass;
        public GeometryNode obj;
        public TransformNode objTransfNode;
        Scene sc;

        public int bounceCount;

        public Tiro(String name,Vector3 inicialPosition, int _mass,int size,Scene scene, MarkerNode groundReference)
        {
            bounceCount = 0;

            animationStartedApplyGravity = false;

            sc = scene;

            obj = new GeometryNode(name);
            _momentum = new Vector3(0, 0, 0);
            _position = inicialPosition;
            obj.Model = new TexturedSphere(size, 20, 20);
            obj.AddToPhysicsEngine = true;
            obj.Physics.Shape = ShapeType.Sphere;
            obj.Model.ShadowAttribute = ShadowAttribute.ReceiveCast;
            obj.Model.Shader = new SimpleShadowShader(scene.ShadowMap);

            obj.Physics.Interactable = true;
            obj.Physics.Collidable = true;
            obj.Physics.Shape = GoblinXNA.Physics.ShapeType.Sphere;
            obj.Physics.Mass = 30f;
            obj.Physics.MaterialName = "CannonBall";
            obj.Physics.ApplyGravity = false;

            objTransfNode = new TransformNode(_position);
            objTransfNode.AddChild(obj);

            Material objMaterial = new Material();
            objMaterial.Diffuse = new Vector4(0, 0.5f, 0, 1);
            objMaterial.Specular = Color.White.ToVector4();
            objMaterial.SpecularPower = 10;

            obj.Material = objMaterial;

            mass = _mass;

            lastTimePositionWasUpdated = DateTime.Now;

            resetBall(groundReference);
        }

        public void addMomentum(Vector3 momentum) // Em relação à posição atual do tiro. Novamente, não à posição real dele.
        {
            _momentum += momentum;
        }

        public void addImpulse_using_Force(Vector3 externalForce, int milisseconds) // Quanto tempo a força vai ser aplicada?
        {
            _momentum += (externalForce / mass) * (milisseconds / 1000f);
        }

        public void addImpulse_using_Mass_and_Velocity(int externalMass, Vector3 externalVelocity) //Considerando que a raquete do jogador tem massa infinita, ou seja, não se mexe com força aplicada... Tem que ver o angulo de contato.
        {
            _momentum += externalVelocity;
        }

        public override void updatePosition(MarkerNode groundReference)
        {
            int deltaT = (DateTime.Now - lastTimePositionWasUpdated).Milliseconds;

            _position += _momentum * deltaT;

            Matrix mat = Matrix.CreateTranslation(_position);

            // Modify the transformation in the physics engine
            ((NewtonPhysics)sc.PhysicsEngine).SetTransform(obj.Physics, mat);

            lastTimePositionWasUpdated = DateTime.Now;

            if (bounceCount > 2)
            {
                resetBall(groundReference);
            }
            //if (_position.LengthSquared() > 1000000)
            //{
            //    resetBall(groundReference);
            //}
            Vector3 groundScale;
            Quaternion groundQuaternion;
            Vector3 groundTranslation;
            groundReference.WorldTransformation.Decompose(out groundScale, out groundQuaternion, out groundTranslation);
            Vector3 ballScale;
            Quaternion ballQuaternion;
            Vector3 ballTranslation;
            obj.Physics.PhysicsWorldTransform.Decompose(out ballScale, out ballQuaternion, out ballTranslation);

            float ball_ground_distance_squared = (groundTranslation - ballTranslation).LengthSquared();

            if ((ballTranslation).LengthSquared() > 400000)
            {
                resetBall(groundReference);
            }
        }

        public void resetBall(MarkerNode groundReference)
        {
            animationStartedApplyGravity = false;
            _position.X = 100;
            _position.Y = 0;
            _position.Z = 100;
            _momentum.X = 0;
            _momentum.Y = 0;
            _momentum.Z = 0;
            bounceCount = 0;

            Matrix mat = Matrix.CreateTranslation(_position);

            ((NewtonPhysics)sc.PhysicsEngine).SetTransform(obj.Physics, mat);
        }
    }
}
