﻿using Accord.Math;
using Accord.Statistics;
using Accord.Statistics.Distributions.Univariate;
using KerasSharp;
using KerasSharp.Backends;
using KerasSharp.Engine.Topology;
using KerasSharp.Losses;
using KerasSharp.Models;
using KerasSharp.Optimizers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TensorFlow;

namespace Tests
{
    [TestFixture]
    public class TensorFlowBackendTest
    {
        [Test]
        public void ndim_test()
        {
            // https://github.com/fchollet/keras/blob/f65a56fb65062c8d14d215c9f4b1015b97cc5bf3/keras/backend/tensorflow_backend.py#L508
            using (var K = new TensorFlowBackend())
            {
                #region doc_ndim
                var input = K.placeholder(shape: new int?[] { 2, 4, 5 });
                double[,] val = new double[,] { { 1, 2 }, { 3, 4 } };
                var kvar = K.variable(array: (Array)val);
                int? a = K.ndim(input); // 3
                int? b = K.ndim(kvar); // 2
                #endregion

                Assert.AreEqual(3, a);
                Assert.AreEqual(2, b);
            }
        }

        [Test]
        public void partial_shape_test()
        {
            // Note: Keras/TensorFlow represent unknown dimensions 
            // as None, whereas TensorFlowSharp represents as -1:
            /*
                import keras

                from keras.models import Sequential
                from keras.layers import Dense
                from keras import backend as K
                import numpy as np

                a = K.placeholder(shape = (None, 2))
                b = K.variable(np.matrix([[ 1, 2, 3], [4, 5, 6]]))
                ab = K.dot(a, b)

                shape_a = K.int_shape(a)
                shape_b = K.int_shape(b)
                shape_ab = K.int_shape(ab)

                print(shape_a)
                print(shape_b)
                print(shape_ab)

                >>> Using TensorFlow backend.
                (None, 2)
                (2, 3)
                (None, 3)
             */

            using (var K = new TensorFlowBackend())
            {
                Tensor a = K.placeholder(shape: new int?[] { null, 2 });
                Tensor b = K.variable(array: new float[,] { { 1, 2, 3 },
                                                            { 4, 5, 6 } });

                var ab = K.dot(a, b);

                int?[] shape_a = K.int_shape(a);
                int?[] shape_b = K.int_shape(b);
                int?[] shape_ab = K.int_shape(ab);

                long[] tf_shape_a = a.TF_Shape;
                long[] tf_shape_b = b.TF_Shape;
                long[] tf_shape_ab = ab.TF_Shape;

                AssertEx.AreEqual(new int?[] { null, 2 }, shape_a);
                AssertEx.AreEqual(new int?[] { 2, 3 }, shape_b);
                AssertEx.AreEqual(new int?[] { null, 3 }, shape_ab);

                AssertEx.AreEqual(new long[] { -1, 2 }, tf_shape_a);
                AssertEx.AreEqual(new long[] { 2, 3 }, tf_shape_b);
                AssertEx.AreEqual(new long[] { -1, 3 }, tf_shape_ab);
            }
        }

        [Test]
        public void softmax_test()
        {
            using (var K = new TensorFlowBackend())
            {
                double[,] a = new double[,] { { -4, 2 }, { 0.02, 0.3 } };
                var ta = K.variable(array: (Array)a, name: "example_var");

                var tr = K.softmax(ta);
                double[,] r = (double[,])tr.eval();

                AssertEx.AreEqual(new double[,] {
                    { 0.0024726231566347748, 0.99752737684336534 },
                    { 0.430453776060771,     0.56954622393922893 } }, r, 1e-8);

                AssertEx.AreEqual(r.GetRow(0), Accord.Math.Special.Softmax(a.GetRow(0)), 1e-8);
                AssertEx.AreEqual(r.GetRow(1), Accord.Math.Special.Softmax(a.GetRow(1)), 1e-8);
            }
        }

        [Test]
        public void variable_test()
        {
            using (var K = new TensorFlowBackend())
            {
                #region doc_variable
                double[,] val = new double[,] { { 1, 2 }, { 3, 4 } };
                var kvar = K.variable(array: (Array)val, name: "example_var");
                var a = K.dtype(kvar); // 'float64'
                var b = kvar.eval(); // { { 1, 2 }, { 3, 4 } }
                #endregion

                Assert.AreEqual(TFDataType.Double, a);
                Assert.AreEqual(new double[,] { { 1, 2 }, { 3, 4 } }, b);
            }
        }

        [Test]
        public void random_uniform()
        {
            using (var K = new TensorFlowBackend())
            {
                #region doc_random_uniform
                var kvar = K.random_uniform(new int?[] { 100, 2000 }, minval: -4, maxval: 2, dtype: TFDataType.Double, seed: 1337, name: "uni");
                var a = K.dtype(kvar); // float64 (Double)
                var b = kvar.eval();
                #endregion

                double[,] actual = (double[,])b;
                Assert.AreEqual(100, actual.Rows());
                Assert.AreEqual(2000, actual.Columns());

                var u = UniformContinuousDistribution.Estimate(actual.Reshape());
                Assert.AreEqual(-4, u.Minimum, 1e-3);
                Assert.AreEqual(+2, u.Maximum, 1e-3);
                Assert.AreEqual(-1, u.Mean, 1e-2);
            }
        }

        [Test]
        public void random_normal_test()
        {
            using (var graph = new TFGraph())
            using (var session = new TFSession(graph))
            {
                TFOutput y = graph.RandomNormal(new TFShape(1000, 100), mean: 42, stddev: 0.4, seed: 1337);

                TFTensor[] result = session.Run(new TFOutput[] { }, new TFTensor[] { }, new[] { y });

                object output = result[0].GetValue();
                double[,] actual = (double[,])output;

                Assert.AreEqual(1000, actual.Rows());
                Assert.AreEqual(100, actual.Columns());

                double actualMean = actual.Mean();
                double expectedMean = 42;
                Assert.AreEqual(expectedMean, actualMean, 1e-2);
            }
        }

        [Test]
        public void int_shape()
        {
            using (var K = new TensorFlowBackend())
            {
                #region doc_int_shape
                var input = K.placeholder(shape: new int?[] { 2, 4, 5 });
                var a = K.int_shape(input); // (2, 4, 5)
                var val = new[,] { { 1, 2 }, { 3, 4 } };
                var kvar = K.variable(array: val);
                var b = K.int_shape(kvar); //(2, 2)
                #endregion

                Assert.AreEqual(new[] { 2, 4, 5 }, a);
                Assert.AreEqual(new[] { 2, 2 }, b);
            }
        }

        [Test]
        public void zeros()
        {
            using (var K = new TensorFlowBackend())
            {
                #region doc_zeros
                var kvar = K.zeros(new int[] { 3, 4 });
                var a = K.eval(kvar); // new[,] {{ 0.,  0.,  0.,  0.},
                                      //         { 0.,  0.,  0.,  0.},
                                      //         { 0.,  0.,  0.,  0.}}
                #endregion

                float[,] actual = (float[,])a;
                Assert.AreEqual(3, actual.Rows());
                Assert.AreEqual(4, actual.Columns());

                double[,] expected = new[,] {{ 0.0,  0.0,  0.0,  0.0},
                                                { 0.0,  0.0,  0.0,  0.0},
                                                { 0.0,  0.0,  0.0,  0.0}};

                Assert.AreEqual(expected, actual);
            }
        }

        [Test]
        public void elementwise_multiplication()
        {
            using (var K = new TensorFlowBackend())
            {
                #region doc_mul
                double[,] a = Matrix.Magic(5);
                double[,] b = Matrix.Identity(5);
                Tensor tb = K.constant(b);
                var kvar = K.mul(a, tb);
                #endregion

                double[,] actual = (double[,])kvar.eval();
                double[,] expected = Elementwise.Multiply(a, b);

                AreEqual(actual, expected);
            }
        }

        private static void AreEqual(double[,] actual, double[,] expected)
        {
            Assert.IsTrue(expected.IsEqual(actual, 1e-10));
        }
    }
}